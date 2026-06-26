using ai_knowledge_assistant.Application.DTOs.Admin;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Features.Admin;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.UnitTests.TestSupport;

namespace ai_knowledge_assistant.UnitTests;

public sealed class AdminAuthorizationTests
{
    [Fact]
    public async Task UpdateUserRole_rejects_unknown_role()
    {
        await using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Email = "user@example.com",
            PasswordHash = "hash",
            RoleId = DefaultRoles.UserRoleId
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new AdminUserService(new UserRepository(context));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateUserRoleAsync(user.Id, new UpdateUserRoleRequest(Guid.NewGuid())));
    }

    [Fact]
    public async Task UpdateUserRole_assigns_valid_role()
    {
        await using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Email = "user@example.com",
            PasswordHash = "hash",
            RoleId = DefaultRoles.UserRoleId
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new AdminUserService(new UserRepository(context));
        var response = await service.UpdateUserRoleAsync(user.Id, new UpdateUserRoleRequest(DefaultRoles.AdminRoleId));

        Assert.Equal(DefaultRoles.Admin, response.Role.Name);
    }
}
