using System.Runtime.CompilerServices;
using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.UnitTests.TestSupport;

internal sealed class FakeJwtTokenService : IJwtTokenService
{
    public AuthToken CreateToken(User user)
    {
        return new AuthToken($"access-token-for-{user.Id}", DateTime.UtcNow.AddMinutes(15));
    }
}

internal sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
{
    private int _counter;

    public RefreshToken CreateToken(User user)
    {
        _counter++;
        return new RefreshToken
        {
            UserId = user.Id,
            User = user,
            Token = $"refresh-token-{_counter}",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
    }
}

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "Fake";

    public int Dimensions => 1536;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var values = new float[Dimensions];
        values[0] = text.Contains("alpha", StringComparison.OrdinalIgnoreCase) ? 1 : 0.1f;
        values[1] = text.Contains("beta", StringComparison.OrdinalIgnoreCase) ? 1 : 0.1f;
        return Task.FromResult(values);
    }
}

internal sealed class FakeTextExtractionService : ITextExtractionService
{
    private readonly string _text;

    public FakeTextExtractionService(string text)
    {
        _text = text;
    }

    public Task<string> ExtractTextAsync(
        string filePath,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_text);
    }
}

internal sealed class FakeSemanticSearchService : ISemanticSearchService
{
    private readonly IReadOnlyCollection<SearchResultResponse> _results;

    public FakeSemanticSearchService(IReadOnlyCollection<SearchResultResponse> results)
    {
        _results = results;
    }

    public bool DocumentContextRequested { get; private set; }

    public int? RequestedContextChunkCount { get; private set; }

    public Task<IReadOnlyCollection<SearchResultResponse>> SearchAsync(
        Guid userId,
        SearchQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_results);
    }

    public Task<IReadOnlyCollection<SearchResultResponse>> GetDocumentContextAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? documentIds,
        int maxChunks,
        CancellationToken cancellationToken = default)
    {
        DocumentContextRequested = true;
        RequestedContextChunkCount = maxChunks;
        return Task.FromResult(_results);
    }
}

internal sealed class FakeAIProvider : IAIProvider
{
    public string Name => "Fake";

    public Task<string> GenerateAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Grounded fake answer [source: alpha.pdf#0]");
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "Grounded ";
        await Task.CompletedTask;
        yield return "fake answer";
    }
}

internal sealed class InMemoryConversationRepository : IConversationRepository
{
    public List<Conversation> Conversations { get; } = [];

    public List<ChatMessage> Messages { get; } = [];

    public Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        Conversations.Add(conversation);
        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetOwnedAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Conversations.FirstOrDefault(conversation =>
            conversation.Id == id && conversation.UserId == userId && !conversation.IsDeleted));
    }

    public Task<PagedResponse<Conversation>> GetOwnedConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = Conversations.Where(conversation => conversation.UserId == userId).ToList();
        return Task.FromResult(new PagedResponse<Conversation>(items, page, pageSize, items.Count));
    }

    public Task<PagedResponse<ChatMessage>> GetOwnedMessagesAsync(
        Guid userId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = Messages.Where(message => message.ConversationId == conversationId).ToList();
        return Task.FromResult(new PagedResponse<ChatMessage>(items, page, pageSize, items.Count));
    }

    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task AddMessagesAsync(
        Conversation conversation,
        IReadOnlyCollection<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (!Conversations.Contains(conversation))
        {
            Conversations.Add(conversation);
        }

        Messages.AddRange(messages);
        conversation.Messages = Messages.Where(message => message.ConversationId == conversation.Id).ToList();
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryChatFeedbackRepository : IChatFeedbackRepository
{
    private readonly ChatMessage? _message;

    public List<ChatFeedback> Feedback { get; } = [];

    public InMemoryChatFeedbackRepository(ChatMessage? message)
    {
        _message = message;
    }

    public Task<ChatMessage?> GetOwnedAssistantMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_message?.Id == messageId ? _message : null);
    }

    public Task<ChatFeedback> AddAsync(ChatFeedback feedback, CancellationToken cancellationToken = default)
    {
        Feedback.Add(feedback);
        return Task.FromResult(feedback);
    }
}
