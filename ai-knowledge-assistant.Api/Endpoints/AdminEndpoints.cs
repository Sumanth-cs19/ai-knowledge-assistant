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

        group.MapGet("/rag/documents", async (
                IRagDiagnosticsService diagnosticsService,
                CancellationToken cancellationToken) =>
            {
                var response = await diagnosticsService.GetDocumentsAsync(cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetRagDiagnosticDocuments{nameSuffix}")
            .WithSummary("Lists indexed document, chunk, and vector diagnostics.")
            .WithOpenApi();

        group.MapGet("/rag/documents/{id:guid}", async (
                Guid id,
                IRagDiagnosticsService diagnosticsService,
                CancellationToken cancellationToken) =>
            {
                var response = await diagnosticsService.GetDocumentAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetRagDiagnosticDocument{nameSuffix}")
            .WithSummary("Returns extracted text, chunks, and embedding diagnostics for one document.")
            .WithOpenApi();

        group.MapGet("/rag/debug/{id:guid}", async (
                Guid id,
                IRagDiagnosticsService diagnosticsService,
                CancellationToken cancellationToken) =>
            {
                var response = await diagnosticsService.GetDocumentAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"DebugRagDocument{nameSuffix}")
            .WithSummary("Temporary Admin-only RAG debug view for a document.")
            .WithOpenApi();

        group.MapPost("/rag/test", async (
                RagTestRequest request,
                IRagDiagnosticsService diagnosticsService,
                CancellationToken cancellationToken) =>
            {
                var response = await diagnosticsService.TestAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"TestRagPipeline{nameSuffix}")
            .WithSummary("Runs retrieval, prompt construction, and the configured AI provider for diagnostics.")
            .WithOpenApi();

        return endpoints;
    }
}
