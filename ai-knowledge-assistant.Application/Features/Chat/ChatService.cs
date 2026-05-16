using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Application.Features.Chat;

public sealed class ChatService : IChatService
{
    private const int ContextChunkCount = 5;
    private readonly IChatHistoryRepository _chatHistoryRepository;
    private readonly ILlmService _llmService;
    private readonly ILogger<ChatService> _logger;
    private readonly ISemanticSearchService _semanticSearchService;

    public ChatService(
        ISemanticSearchService semanticSearchService,
        ILlmService llmService,
        IChatHistoryRepository chatHistoryRepository,
        ILogger<ChatService> logger)
    {
        _semanticSearchService = semanticSearchService;
        _llmService = llmService;
        _chatHistoryRepository = chatHistoryRepository;
        _logger = logger;
    }

    public async Task<ChatResponse> AskAsync(
        Guid userId,
        ChatAskRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(userId, request);

        _logger.LogInformation("Processing chat request for user {UserId}", userId);
        var matches = await _semanticSearchService.SearchAsync(
            userId,
            new SearchQueryRequest(request.Question, ContextChunkCount),
            cancellationToken);

        if (matches.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["context"] = ["No relevant document chunks were found for this question."]
            });
        }

        var prompt = BuildPrompt(request.Question, matches);
        var answer = await _llmService.GenerateAnswerAsync(
            prompt,
            matches.Select(match => match.Content).ToList(),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("The language model did not return an answer.");
        }

        var sources = matches.Select(ToSource).ToList();
        var chatMessage = new ChatMessage
        {
            UserId = userId,
            Question = request.Question.Trim(),
            Answer = answer,
            SourceReferencesJson = JsonSerializer.Serialize(sources),
            CreatedAt = DateTime.UtcNow
        };

        await _chatHistoryRepository.AddAsync(chatMessage, cancellationToken);
        _logger.LogInformation(
            "Completed chat request {ChatMessageId} for user {UserId} with {SourceCount} sources",
            chatMessage.Id,
            userId,
            sources.Count);

        return new ChatResponse(
            chatMessage.Id,
            chatMessage.Question,
            chatMessage.Answer,
            chatMessage.CreatedAt,
            sources);
    }

    public async IAsyncEnumerable<string> AskStreamAsync(
        Guid userId,
        ChatAskRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(userId, request);

        _logger.LogInformation("Processing streaming chat request for user {UserId}", userId);
        var matches = await _semanticSearchService.SearchAsync(
            userId,
            new SearchQueryRequest(request.Question, ContextChunkCount),
            cancellationToken);

        if (matches.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["context"] = ["No relevant document chunks were found for this question."]
            });
        }

        var prompt = BuildPrompt(request.Question, matches);
        var answerBuilder = new StringBuilder();

        await foreach (var token in _llmService.StreamAnswerAsync(
                           prompt,
                           matches.Select(match => match.Content).ToList(),
                           cancellationToken))
        {
            answerBuilder.Append(token);
            yield return token;
        }

        var answer = answerBuilder.ToString();
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("The language model did not return an answer.");
        }

        var sources = matches.Select(ToSource).ToList();
        var chatMessage = new ChatMessage
        {
            UserId = userId,
            Question = request.Question.Trim(),
            Answer = answer,
            SourceReferencesJson = JsonSerializer.Serialize(sources),
            CreatedAt = DateTime.UtcNow
        };

        await _chatHistoryRepository.AddAsync(chatMessage, cancellationToken);
        _logger.LogInformation(
            "Completed streaming chat request {ChatMessageId} for user {UserId} with {SourceCount} sources",
            chatMessage.Id,
            userId,
            sources.Count);
    }

    public async Task<IReadOnlyCollection<ChatHistoryResponse>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedRequestException("Authenticated user id is missing or invalid.");
        }

        var messages = await _chatHistoryRepository.GetUserHistoryAsync(userId, cancellationToken);

        return messages
            .Select(message => new ChatHistoryResponse(
                message.Id,
                message.Question,
                message.Answer,
                message.CreatedAt,
                DeserializeSources(message.SourceReferencesJson)))
            .ToList();
    }

    private static string BuildPrompt(string question, IReadOnlyCollection<SearchResultResponse> matches)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an AI knowledge assistant. Answer the question using only the provided document context.");
        prompt.AppendLine("If the context does not contain the answer, say that the uploaded documents do not contain enough information.");
        prompt.AppendLine("Cite sources inline using [source: original-file-name#chunk-index].");
        prompt.AppendLine();
        prompt.AppendLine("Question:");
        prompt.AppendLine(question.Trim());
        prompt.AppendLine();
        prompt.AppendLine("Context:");

        foreach (var match in matches)
        {
            prompt.AppendLine($"[source: {match.OriginalFileName}#{match.ChunkIndex}]");
            prompt.AppendLine(match.Content);
            prompt.AppendLine();
        }

        return prompt.ToString();
    }

    private static void ValidateRequest(Guid userId, ChatAskRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (userId == Guid.Empty)
        {
            errors[nameof(userId)] = ["An authenticated user is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            errors[nameof(request.Question)] = ["Question is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static ChatSourceResponse ToSource(SearchResultResponse match)
    {
        return new ChatSourceResponse(
            match.DocumentId,
            match.ChunkId,
            match.ChunkIndex,
            match.OriginalFileName,
            match.Similarity);
    }

    private static IReadOnlyCollection<ChatSourceResponse> DeserializeSources(string sourceReferencesJson)
    {
        if (string.IsNullOrWhiteSpace(sourceReferencesJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<IReadOnlyCollection<ChatSourceResponse>>(sourceReferencesJson) ?? [];
    }
}
