using System.Security.Claims;
using System.Text.Json;
using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chat")
            .RequireAuthorization()
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
            .WithName("AskChat")
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

                await foreach (var token in chatService.AskStreamAsync(userId, request, cancellationToken))
                {
                    var payload = JsonSerializer.Serialize(new { token });
                    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }

                await context.Response.WriteAsync("event: done\ndata: {}\n\n", cancellationToken);
            })
            .WithName("AskChatStream")
            .WithOpenApi();

        group.MapGet("/history", async (
                HttpContext context,
                IChatService chatService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await chatService.GetHistoryAsync(userId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetChatHistory")
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
