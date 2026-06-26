namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record FeedbackRatingBreakdownResponse(
    int Rating,
    int Count);
