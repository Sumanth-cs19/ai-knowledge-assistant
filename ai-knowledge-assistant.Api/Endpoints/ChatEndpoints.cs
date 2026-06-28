using System.Security.Claims;
using System.Text.Json;
using ai_knowledge_assistant.Api.Authorization;
using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/chat",
        string nameSuffix = "")
    {
        var group = endpoints.MapGroup(prefix)
            .RequireAuthorization(AuthorizationPolicies.RequireAuthenticatedUser)
            .RequireRateLimiting("chat")
            .WithTags("Chat");

        group.MapPost("/ask", async (
                ChatAskRequest request,
                HttpContext context,
                IChatService chatService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await chatService.AskAsync(userId, request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"AskChat{nameSuffix}")
            .Produces<ChatResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithOpenApi();

        group.MapPost("/ask/stream", async (
                ChatAskRequest request,
                HttpContext context,
                IChatService chatService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";
                context.Response.ContentType = "text/event-stream";

                await foreach (var streamEvent in chatService.AskStreamAsync(userId, request, cancellationToken))
                {
                    var payload = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);
                    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }

                await context.Response.WriteAsync("event: done\ndata: {}\n\n", cancellationToken);
            })
            .WithName($"AskChatStream{nameSuffix}")
            .WithOpenApi();

        group.MapPost("/messages/{messageId:guid}/feedback", async (
                Guid messageId,
                ChatFeedbackRequest request,
                HttpContext context,
                IChatFeedbackService feedbackService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await feedbackService.SubmitAsync(userId, messageId, request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"SubmitChatFeedback{nameSuffix}")
            .Produces<ChatFeedbackResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
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
