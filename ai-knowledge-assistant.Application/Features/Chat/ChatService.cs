using System.Diagnostics;
using System.Text;
using System.Runtime.CompilerServices;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Application.Features.Chat;

public sealed class ChatService : IChatService
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private const int ContextChunkCount = 5;
    private const int SummaryContextChunkCount = 12;
    private readonly IAIProvider _aiProvider;
    private readonly IConversationRepository _conversationRepository;
    private readonly ILogger<ChatService> _logger;
    private readonly ISemanticSearchService _semanticSearchService;

    public ChatService(
        ISemanticSearchService semanticSearchService,
        IAIProvider aiProvider,
        IConversationRepository conversationRepository,
        ILogger<ChatService> logger)
    {
        _semanticSearchService = semanticSearchService;
        _aiProvider = aiProvider;
        _conversationRepository = conversationRepository;
        _logger = logger;
    }

    public async Task<ChatResponse> AskAsync(
        Guid userId,
        ChatAskRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(userId, request);

        using var activity = ActivitySource.StartActivity("chat.ask");
        activity?.SetTag("user.id", userId);
        _logger.LogInformation("Processing chat request for user {UserId}", userId);
        var (matches, summarizationMode) = await RetrieveContextAsync(userId, request, cancellationToken);
        activity?.SetTag("chat.summarization_mode", summarizationMode);
        activity?.SetTag("chat.citation_count", matches.Count);
        var answer = RagPromptBuilder.NoContextAnswer;

        if (matches.Count > 0)
        {
            var prompt = RagPromptBuilder.Build(request.Question, matches, summarizationMode);
            _logger.LogDebug("Final RAG prompt for user {UserId}: {Prompt}", userId, prompt);
            answer = await _aiProvider.GenerateAnswerAsync(
                prompt,
                matches.Select(match => match.Content).ToList(),
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("The language model did not return an answer.");
        }

        return await SaveResponseAsync(userId, request, answer, matches, cancellationToken);
    }

    public async IAsyncEnumerable<ChatStreamEvent> AskStreamAsync(
        Guid userId,
        ChatAskRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(userId, request);

        using var activity = ActivitySource.StartActivity("chat.ask_stream");
        activity?.SetTag("user.id", userId);
        _logger.LogInformation("Processing streaming chat request for user {UserId}", userId);
        var (matches, summarizationMode) = await RetrieveContextAsync(userId, request, cancellationToken);
        activity?.SetTag("chat.summarization_mode", summarizationMode);
        activity?.SetTag("chat.citation_count", matches.Count);
        var answerBuilder = new StringBuilder();

        if (matches.Count == 0)
        {
            answerBuilder.Append(RagPromptBuilder.NoContextAnswer);
            yield return new ChatStreamEvent("token", RagPromptBuilder.NoContextAnswer);
        }
        else
        {
            var prompt = RagPromptBuilder.Build(request.Question, matches, summarizationMode);
            _logger.LogDebug("Final streaming RAG prompt for user {UserId}: {Prompt}", userId, prompt);
            await foreach (var token in _aiProvider.StreamAnswerAsync(
                               prompt,
                               matches.Select(match => match.Content).ToList(),
                               cancellationToken))
            {
                answerBuilder.Append(token);
                yield return new ChatStreamEvent("token", token);
            }
        }

        var answer = answerBuilder.ToString();
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("The language model did not return an answer.");
        }

        var response = await SaveResponseAsync(userId, request, answer, matches, cancellationToken);

        yield return new ChatStreamEvent(
            "complete",
            Response: response);
    }

    public static bool IsSummarizationQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return RagPromptBuilder.IsBroadContextQuestion(question);
    }

    private async Task<(IReadOnlyCollection<SearchResultResponse> Matches, bool SummarizationMode)> RetrieveContextAsync(
        Guid userId,
        ChatAskRequest request,
        CancellationToken cancellationToken)
    {
        var summarizationMode = IsSummarizationQuestion(request.Question);
        var documentIds = GetRequestedDocumentIds(request);
        var matches = summarizationMode
            ? await _semanticSearchService.GetDocumentContextAsync(
                userId,
                documentIds,
                SummaryContextChunkCount,
                cancellationToken)
            : await _semanticSearchService.SearchAsync(
                userId,
                new SearchQueryRequest(request.Question, ContextChunkCount, documentIds),
                cancellationToken);

        _logger.LogInformation(
            "Chat context selected for user {UserId}. SummarizationMode={SummarizationMode}. RequestedDocumentIds={RequestedDocumentIds}. SelectedChunks={SelectedChunks}. SimilarityScores={SimilarityScores}",
            userId,
            summarizationMode,
            documentIds ?? [],
            matches.Select(match => match.ChunkId).ToArray(),
            matches.Select(match => Math.Round(match.Similarity, 4)).ToArray());

        return (matches, summarizationMode);
    }

    private async Task<ChatResponse> SaveResponseAsync(
        Guid userId,
        ChatAskRequest request,
        string answer,
        IReadOnlyCollection<SearchResultResponse> matches,
        CancellationToken cancellationToken)
    {
        var conversation = await GetOrCreateConversationAsync(userId, request, cancellationToken);
        var now = DateTime.UtcNow;
        var userMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = ChatMessageRole.User,
            Content = request.Question.Trim(),
            TokenCount = EstimateTokenCount(request.Question),
            CreatedAt = now
        };
        var assistantMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = ChatMessageRole.Assistant,
            Content = answer,
            TokenCount = EstimateTokenCount(answer),
            CreatedAt = DateTime.UtcNow
        };

        conversation.UpdatedAt = assistantMessage.CreatedAt;
        await _conversationRepository.AddMessagesAsync(conversation, [userMessage, assistantMessage], cancellationToken);
        var sources = matches.Select(ToSource).ToList();
        _logger.LogInformation(
            "Completed chat request for conversation {ConversationId} and user {UserId} with {SourceCount} sources",
            conversation.Id,
            userId,
            sources.Count);

        return new ChatResponse(
            conversation.Id,
            userMessage.Id,
            assistantMessage.Id,
            userMessage.Content,
            assistantMessage.Content,
            assistantMessage.CreatedAt,
            sources);
    }

    private static IReadOnlyCollection<Guid>? GetRequestedDocumentIds(ChatAskRequest request)
    {
        var documentIds = (request.SelectedDocumentIds ?? [])
            .Append(request.DocumentId ?? Guid.Empty)
            .Where(documentId => documentId != Guid.Empty)
            .Distinct()
            .ToArray();

        return documentIds.Length == 0 ? null : documentIds;
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
            match.OriginalFileName,
            match.Similarity,
            CreateSnippet(match.Content),
            match.ScoreType);
    }

    private static string CreateSnippet(string content)
    {
        const int maxLength = 220;
        var normalized = string.Join(' ', content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength].Trim()}...";
    }

    private async Task<Conversation> GetOrCreateConversationAsync(
        Guid userId,
        ChatAskRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId.HasValue)
        {
            return await _conversationRepository.GetOwnedAsync(userId, request.ConversationId.Value, cancellationToken)
                ?? throw new NotFoundException("Conversation was not found.");
        }

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            UserId = userId,
            Title = GenerateTitle(request.Question),
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _conversationRepository.AddAsync(conversation, cancellationToken);
    }

    private static string GenerateTitle(string question)
    {
        const int maxLength = 60;
        var normalized = string.Join(' ', question.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength].Trim()}...";
    }

    private static int EstimateTokenCount(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(content.Length / 4.0));
    }
}
