namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatFeedbackResponse(
    Guid Id,
    Guid ChatMessageId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);
