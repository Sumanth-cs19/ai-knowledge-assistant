namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record AnalyticsOverviewResponse(
    int TotalUsers,
    int ActiveUsers,
    int TotalDocuments,
    int IndexedDocuments,
    int FailedDocuments,
    int TotalConversations,
    int TotalChatMessages,
    double? AverageFeedbackRating);
