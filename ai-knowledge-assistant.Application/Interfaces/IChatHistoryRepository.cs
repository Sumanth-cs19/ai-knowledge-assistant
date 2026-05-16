using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IChatHistoryRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChatMessage>> GetUserHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
}
