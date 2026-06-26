namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatAskRequest(
    string Question,
    Guid? ConversationId = null);
