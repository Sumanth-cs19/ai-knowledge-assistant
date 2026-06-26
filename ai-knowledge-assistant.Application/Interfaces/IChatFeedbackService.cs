using ai_knowledge_assistant.Application.DTOs.Chat;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IChatFeedbackService
{
    Task<ChatFeedbackResponse> SubmitAsync(
        Guid userId,
        Guid messageId,
        ChatFeedbackRequest request,
        CancellationToken cancellationToken = default);
}
