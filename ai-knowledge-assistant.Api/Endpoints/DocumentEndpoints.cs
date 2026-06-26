using System.Security.Claims;
using ai_knowledge_assistant.Api.Authorization;
using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/documents",
        string nameSuffix = "")
    {
        var group = endpoints.MapGroup(prefix)
            .RequireAuthorization(AuthorizationPolicies.RequireAuthenticatedUser)
            .WithTags("Documents");

        group.MapPost("/upload", async (
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);

                if (!context.Request.HasFormContentType)
                {
                    throw new ValidationException(new Dictionary<string, string[]>
                    {
                        ["file"] = ["A multipart form upload with a file field named 'file' is required."]
                    });
                }

                var form = await context.Request.ReadFormAsync(cancellationToken);
                var file = form.Files.GetFile("file");

                if (file is null)
                {
                    throw new ValidationException(new Dictionary<string, string[]>
                    {
                        ["file"] = ["A file field named 'file' is required."]
                    });
                }

                await using var stream = file.OpenReadStream();
                var request = new UploadDocumentRequest(
                    userId,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    stream);

                var response = await documentService.UploadAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"UploadDocument{nameSuffix}")
            .Accepts<IFormFile>("multipart/form-data")
            .WithOpenApi();

        group.MapGet("/my-documents", async (
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await documentService.GetUserDocumentsAsync(userId, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetMyDocuments{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/", async (
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await documentService.GetUserDocumentsAsync(userId, page, pageSize, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetDocuments{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await documentService.GetAsync(userId, id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetDocument{nameSuffix}")
            .WithOpenApi();

        group.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                await documentService.DeleteAsync(userId, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName($"DeleteDocument{nameSuffix}")
            .WithOpenApi();

        group.MapPost("/{id:guid}/reindex", async (
                Guid id,
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                await documentService.ReindexAsync(userId, id, cancellationToken);
                return Results.Accepted($"{prefix}/{id}");
            })
            .WithName($"ReindexDocument{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/{id:guid}/versions", async (
                Guid id,
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await documentService.GetVersionsAsync(userId, id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetDocumentVersions{nameSuffix}")
            .WithOpenApi();

        group.MapGet("/{id:guid}/chunks", async (
                Guid id,
                HttpContext context,
                IDocumentService documentService,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 50) =>
            {
                var userId = GetCurrentUserId(context);
                var response = await documentService.GetChunksAsync(userId, id, page, pageSize, cancellationToken);
                return Results.Ok(response);
            })
            .WithName($"GetDocumentChunks{nameSuffix}")
            .WithOpenApi();

        return endpoints;
    }

    private static Guid GetCurrentUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedRequestException("Authenticated user id is missing or invalid.");
        }

        return userId;
    }
}
