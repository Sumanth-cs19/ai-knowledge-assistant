using System.Security.Cryptography;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private readonly JwtSettings _jwtSettings;

    public RefreshTokenGenerator(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public RefreshToken CreateToken(User user)
    {
        return new RefreshToken
        {
            UserId = user.Id,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
