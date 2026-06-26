using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IChatFeedbackRepository
{
    Task<ChatMessage?> GetOwnedAssistantMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<ChatFeedback> AddAsync(ChatFeedback feedback, CancellationToken cancellationToken = default);
}
