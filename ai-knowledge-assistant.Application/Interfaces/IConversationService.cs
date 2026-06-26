using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Application.DTOs.Conversations;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IConversationService
{
    Task<ConversationResponse> CreateAsync(
        Guid userId,
        ConversationCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ConversationResponse>> GetConversationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ConversationResponse> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ConversationResponse> UpdateAsync(
        Guid userId,
        Guid id,
        ConversationUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<PagedResponse<ChatMessageResponse>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
