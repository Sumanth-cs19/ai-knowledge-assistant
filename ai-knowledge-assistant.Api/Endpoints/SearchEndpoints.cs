using System.Security.Claims;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/search")
            .RequireAuthorization()
            .WithTags("Search");

        group.MapPost("/query", async (
                SearchQueryRequest request,
                HttpContext context,
                ISemanticSearchService semanticSearchService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await semanticSearchService.SearchAsync(userId, request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("SemanticSearch")
            .WithOpenApi();

        return endpoints;
    }

    private static Guid GetCurrentUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedRequestException("Authenticated user id is missing or invalid.");
        }

        return userId;
    }
}
