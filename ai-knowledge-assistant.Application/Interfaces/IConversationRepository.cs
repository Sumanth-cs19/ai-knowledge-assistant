using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IConversationRepository
{
    Task<Conversation> AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task<Conversation?> GetOwnedAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PagedResponse<Conversation>> GetOwnedConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ChatMessage>> GetOwnedMessagesAsync(
        Guid userId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task AddMessagesAsync(
        Conversation conversation,
        IReadOnlyCollection<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
