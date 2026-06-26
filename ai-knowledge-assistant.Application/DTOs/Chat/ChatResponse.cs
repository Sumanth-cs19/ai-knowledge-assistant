namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatResponse(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string Question,
    string Answer,
    DateTime CreatedAt,
    IReadOnlyCollection<ChatSourceResponse> Citations)
{
    public IReadOnlyCollection<ChatSourceResponse> Sources => Citations;
}
