using System.Security.Claims;
using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/documents")
            .RequireAuthorization()
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
            .WithName("UploadDocument")
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
            .WithName("GetMyDocuments")
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
