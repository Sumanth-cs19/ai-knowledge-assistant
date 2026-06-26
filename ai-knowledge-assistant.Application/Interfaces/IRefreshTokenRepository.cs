using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task RotateAsync(
        RefreshToken oldRefreshToken,
        RefreshToken newRefreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
