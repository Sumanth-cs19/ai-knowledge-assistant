using ai_knowledge_assistant.Application.DTOs.Auth;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", async (
                RegisterRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var response = await authService.RegisterAsync(request, cancellationToken);
                return Results.Created("/api/auth/login", response);
            })
            .AllowAnonymous()
            .WithName("Register")
            .WithOpenApi();

        group.MapPost("/login", async (
                LoginRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var response = await authService.LoginAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName("Login")
            .WithOpenApi();

        return endpoints;
    }
}
