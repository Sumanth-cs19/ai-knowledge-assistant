using ai_knowledge_assistant.Application.DTOs.Admin;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyCollection<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateUserRoleAsync(
        Guid id,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}
