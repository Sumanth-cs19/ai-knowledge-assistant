using ai_knowledge_assistant.Application.DTOs.Admin;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Features.Admin;

public sealed class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;

    public AdminUserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyCollection<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    public async Task<UserResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateUserRoleAsync(
        Guid id,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RoleId == Guid.Empty || !await _userRepository.RoleExistsAsync(request.RoleId, cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.RoleId)] = ["A valid role id is required."]
            });
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        user.RoleId = request.RoleId;
        await _userRepository.UpdateAsync(user, cancellationToken);

        var updatedUser = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        return ToResponse(updatedUser);
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        await _userRepository.DeleteAsync(user, cancellationToken);
    }

    private static UserResponse ToResponse(User user)
    {
        if (user.Role is null)
        {
            throw new InvalidOperationException("User role was not loaded.");
        }

        return new UserResponse(
            user.Id,
            user.Email,
            user.CreatedAt,
            new RoleResponse(user.Role.Id, user.Role.Name, user.Role.Description));
    }
}
