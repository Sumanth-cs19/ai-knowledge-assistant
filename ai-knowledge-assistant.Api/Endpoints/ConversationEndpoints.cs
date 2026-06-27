using System.Security.Claims;
using ai_knowledge_assistant.Api.Authorization;
using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Application.DTOs.Conversations;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/conversations",
        string nameSuffix = "")
    {
        var group = endpoints.MapGroup(prefix)
            .RequireAuthorization(AuthorizationPolicies.RequireAuthenticatedUser)
            .WithTags("Conversations");

        group.MapPost("/", async (
                ConversationCreateRequest request,
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await conversationService.CreateAsync(userId, request, cancellationToken);
                return Results.Created($"{prefix}/{response.Id}", response);
            })
            .WithName($"CreateConversation{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/", async (
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await conversationService.GetConversationsAsync(userId, page, pageSize, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetConversations{nameSuffix}")
            .Produces<PagedResponse<ConversationResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi();

        group.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await conversationService.GetAsync(userId, id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetConversation{nameSuffix}")
            .WithOpenApi();

        group.MapPut("/{id:guid}", async (
                Guid id,
                ConversationUpdateRequest request,
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await conversationService.UpdateAsync(userId, id, request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"UpdateConversation{nameSuffix}")
            .WithOpenApi();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                await conversationService.DeleteAsync(userId, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName($"DeleteConversation{nameSuffix}")
            .WithOpenApi();

        group.MapPost("/{id:guid}/archive", async (
                Guid id,
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                await conversationService.ArchiveAsync(userId, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName($"ArchiveConversation{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/{id:guid}/messages", async (
                Guid id,
                HttpContext context,
                IConversationService conversationService,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await conversationService.GetMessagesAsync(userId, id, page, pageSize, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetConversationMessages{nameSuffix}")
            .Produces<PagedResponse<ChatMessageResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
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
