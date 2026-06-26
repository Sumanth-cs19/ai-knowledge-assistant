using ai_knowledge_assistant.Api.Authorization;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class AdminAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAdminAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapAnalyticsGroup(endpoints, "/api/admin/analytics", "AdminAnalytics");
        MapAnalyticsGroup(endpoints, "/api/v1/admin/analytics", "AdminAnalyticsV1");
        return endpoints;
    }

    private static void MapAnalyticsGroup(IEndpointRouteBuilder endpoints, string prefix, string namePrefix)
    {
        var group = endpoints.MapGroup(prefix)
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin)
            .WithTags("Admin Analytics");

        group.MapGet("/overview", async (
                IAdminAnalyticsService analyticsService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                loggerFactory.CreateLogger("AdminAnalytics")
                    .LogInformation("Admin analytics overview endpoint accessed");
                return Results.Ok(await analyticsService.GetOverviewAsync(cancellationToken));
            })
            .WithName($"{namePrefix}Overview")
            .WithOpenApi();

        group.MapGet("/users", async (
                IAdminAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await analyticsService.GetUsersAsync(cancellationToken)))
            .WithName($"{namePrefix}Users")
            .WithOpenApi();

        group.MapGet("/documents", async (
                IAdminAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await analyticsService.GetDocumentsAsync(cancellationToken)))
            .WithName($"{namePrefix}Documents")
            .WithOpenApi();

        group.MapGet("/chats", async (
                IAdminAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await analyticsService.GetChatsAsync(cancellationToken)))
            .WithName($"{namePrefix}Chats")
            .WithOpenApi();

        group.MapGet("/feedback", async (
                IAdminAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await analyticsService.GetFeedbackAsync(cancellationToken)))
            .WithName($"{namePrefix}Feedback")
            .WithOpenApi();
    }
}
