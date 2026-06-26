namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record UserAnalyticsResponse(
    int TotalUsers,
    int ActiveUsers,
    int NewUsersLast7Days,
    int NewUsersLast30Days);
