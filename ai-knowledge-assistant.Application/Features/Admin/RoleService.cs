using ai_knowledge_assistant.Application.DTOs.Admin;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Application.Features.Admin;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<IReadOnlyCollection<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetAllAsync(cancellationToken);
        return roles
            .Select(role => new RoleResponse(role.Id, role.Name, role.Description))
            .ToList();
    }
}
