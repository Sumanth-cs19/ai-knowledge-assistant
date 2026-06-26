namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatFeedbackRequest(
    int Rating,
    string? Comment = null);
