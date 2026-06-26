using ai_knowledge_assistant.Application.DTOs.Admin;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IAdminAnalyticsService
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<UserAnalyticsResponse> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<DocumentAnalyticsResponse> GetDocumentsAsync(CancellationToken cancellationToken = default);

    Task<ChatAnalyticsResponse> GetChatsAsync(CancellationToken cancellationToken = default);

    Task<FeedbackAnalyticsResponse> GetFeedbackAsync(CancellationToken cancellationToken = default);
}
