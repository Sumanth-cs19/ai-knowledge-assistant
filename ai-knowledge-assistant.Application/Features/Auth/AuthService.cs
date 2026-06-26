using System.Net.Mail;
using ai_knowledge_assistant.Application.DTOs.Auth;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Application.Features.Auth;

public sealed class AuthService : IAuthService
{
    private const int MinimumPasswordLength = 8;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidateCredentials(email, request.Password);

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleId = DefaultRoles.UserRoleId
        };

        await _userRepository.AddAsync(user, cancellationToken);
        _logger.LogInformation("User registered with id {UserId}", user.Id);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidateCredentials(email, request.Password);

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email {Email}", email);
            throw new UnauthorizedRequestException("Invalid email or password.");
        }

        _logger.LogInformation("User {UserId} logged in", user.Id);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTokenRequest(request.RefreshToken);

        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive || refreshToken.User is null)
        {
            throw new UnauthorizedRequestException("Invalid or expired refresh token.");
        }

        Revoke(refreshToken);
        var newRefreshToken = _refreshTokenGenerator.CreateToken(refreshToken.User);
        await _refreshTokenRepository.RotateAsync(refreshToken, newRefreshToken, cancellationToken);
        _logger.LogInformation("Refresh token rotated for user {UserId}", refreshToken.User.Id);

        var accessToken = _jwtTokenService.CreateToken(refreshToken.User);
        return new AuthResponse(
            accessToken.Value,
            newRefreshToken.Token,
            refreshToken.User.Email,
            accessToken.ExpiresAt,
            newRefreshToken.ExpiresAt);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTokenRequest(request.RefreshToken);

        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (refreshToken is null)
        {
            return;
        }

        if (!refreshToken.IsRevoked)
        {
            Revoke(refreshToken);
            await _refreshTokenRepository.RevokeAsync(refreshToken, cancellationToken);
            _logger.LogInformation("Refresh token revoked for user {UserId}", refreshToken.UserId);
        }
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _jwtTokenService.CreateToken(user);
        var refreshToken = _refreshTokenGenerator.CreateToken(user);
        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        return new AuthResponse(
            accessToken.Value,
            refreshToken.Token,
            user.Email,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt);
    }

    private static void Revoke(RefreshToken refreshToken)
    {
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static void ValidateCredentials(string email, string? password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            errors[nameof(RegisterRequest.Email)] = ["A valid email address is required."];
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
        {
            errors[nameof(RegisterRequest.Password)] = [$"Password must be at least {MinimumPasswordLength} characters long."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static void ValidateTokenRequest(string? refreshToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        throw new ValidationException(new Dictionary<string, string[]>
        {
            [nameof(RefreshTokenRequest.RefreshToken)] = ["Refresh token is required."]
        });
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
