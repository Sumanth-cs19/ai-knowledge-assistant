using ai_knowledge_assistant.Application.DTOs.Auth;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Features.Auth;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.Infrastructure.Services;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace ai_knowledge_assistant.UnitTests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Register_assigns_default_user_role_and_returns_tokens()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var response = await service.RegisterAsync(new RegisterRequest("New.User@Example.com", "Password123!"));

        var user = context.Users.Single();
        Assert.Equal("new.user@example.com", user.Email);
        Assert.Equal(DefaultRoles.UserRoleId, user.RoleId);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task Login_rejects_invalid_password()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        await service.RegisterAsync(new RegisterRequest("user@example.com", "Password123!"));

        await Assert.ThrowsAsync<UnauthorizedRequestException>(() =>
            service.LoginAsync(new LoginRequest("user@example.com", "wrong-password")));
    }

    [Fact]
    public async Task Refresh_rotates_refresh_token_and_revokes_old_token()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        var registered = await service.RegisterAsync(new RegisterRequest("user@example.com", "Password123!"));

        var refreshed = await service.RefreshAsync(new RefreshTokenRequest(registered.RefreshToken));

        var oldToken = context.RefreshTokens.Single(token => token.Token == registered.RefreshToken);
        Assert.True(oldToken.IsRevoked);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
        Assert.Contains(context.RefreshTokens, token => token.Token == refreshed.RefreshToken && !token.IsRevoked);
    }

    [Fact]
    public async Task Logout_revokes_existing_refresh_token()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        var registered = await service.RegisterAsync(new RegisterRequest("user@example.com", "Password123!"));

        await service.LogoutAsync(new LogoutRequest(registered.RefreshToken));

        Assert.True(context.RefreshTokens.Single().IsRevoked);
    }

    private static AuthService CreateService(ApplicationDbContext context)
    {
        return new AuthService(
            new UserRepository(context),
            new BCryptPasswordHasher(),
            new FakeJwtTokenService(),
            new FakeRefreshTokenGenerator(),
            new RefreshTokenRepository(context),
            NullLogger<AuthService>.Instance);
    }
}
