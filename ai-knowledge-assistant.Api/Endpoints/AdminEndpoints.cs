using ai_knowledge_assistant.Api.Authorization;
using ai_knowledge_assistant.Application.DTOs.Admin;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/admin",
        string nameSuffix = "")
    {
        var group = endpoints.MapGroup(prefix)
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin)
            .WithTags("Admin");

        group.MapGet("/users", async (
                IAdminUserService adminUserService,
                CancellationToken cancellationToken) =>
            {
                var response = await adminUserService.GetUsersAsync(cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetAdminUsers{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/users/{id:guid}", async (
                Guid id,
                IAdminUserService adminUserService,
                CancellationToken cancellationToken) =>
            {
                var response = await adminUserService.GetUserAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetAdminUser{nameSuffix}")
            .WithOpenApi();

        group.MapPut("/users/{id:guid}/role", async (
                Guid id,
                UpdateUserRoleRequest request,
                IAdminUserService adminUserService,
                CancellationToken cancellationToken) =>
            {
                var response = await adminUserService.UpdateUserRoleAsync(id, request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"UpdateAdminUserRole{nameSuffix}")
            .WithOpenApi();

        group.MapDelete("/users/{id:guid}", async (
                Guid id,
                IAdminUserService adminUserService,
                CancellationToken cancellationToken) =>
            {
                await adminUserService.DeleteUserAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName($"DeleteAdminUser{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/roles", async (
                IRoleService roleService,
                CancellationToken cancellationToken) =>
            {
                var response = await roleService.GetRolesAsync(cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetAdminRoles{nameSuffix}")
            .WithOpenApi();

        return endpoints;
    }
}
