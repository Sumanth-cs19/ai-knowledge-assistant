using ai_knowledge_assistant.Application.DTOs.Auth;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/auth",
        string nameSuffix = "")
    {
        var group = endpoints.MapGroup(prefix)
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
            .WithName($"Register{nameSuffix}")
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
            .WithName($"Login{nameSuffix}")
            .WithOpenApi();

        group.MapPost("/refresh", async (
                RefreshTokenRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var response = await authService.RefreshAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName($"RefreshToken{nameSuffix}")
            .WithOpenApi();

        group.MapPost("/logout", async (
                LogoutRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                await authService.LogoutAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName($"Logout{nameSuffix}")
            .WithOpenApi();

        return endpoints;
    }
}
