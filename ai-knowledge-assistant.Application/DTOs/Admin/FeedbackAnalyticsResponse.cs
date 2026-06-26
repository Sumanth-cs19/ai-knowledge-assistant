namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record FeedbackAnalyticsResponse(
    int TotalFeedback,
    double? AverageRating,
    int PositiveFeedback,
    int NegativeFeedback,
    IReadOnlyCollection<FeedbackRatingBreakdownResponse> RatingBreakdown);
