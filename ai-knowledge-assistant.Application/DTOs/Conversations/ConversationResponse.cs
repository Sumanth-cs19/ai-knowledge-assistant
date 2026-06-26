namespace ai_knowledge_assistant.Application.DTOs.Conversations;

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived);
