using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private readonly ApplicationDbContext _context;
    private readonly IDocumentIndexingService _documentIndexingService;
    private readonly StorageSettings _storageSettings;

    public DocumentService(
        ApplicationDbContext context,
        IDocumentIndexingService documentIndexingService,
        IOptions<StorageSettings> storageSettings)
    {
        _context = context;
        _documentIndexingService = documentIndexingService;
        _storageSettings = storageSettings.Value;
    }

    public async Task<DocumentResponse> UploadAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadRequest(request);

        var userExists = await _context.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new UnauthorizedRequestException("Authenticated user could not be found.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        var safeOriginalFileName = Path.GetFileName(request.OriginalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var fullFilePath = Path.Combine(uploadsPath, storedFileName);

        await using (var fileStream = new FileStream(
                         fullFilePath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 81920,
                         useAsync: true))
        {
            if (request.Content.CanSeek)
            {
                request.Content.Position = 0;
            }

            await request.Content.CopyToAsync(fileStream, cancellationToken);
        }

        var document = new Document
        {
            UserId = request.UserId,
            FileName = storedFileName,
            OriginalFileName = safeOriginalFileName,
            ContentType = request.ContentType,
            FilePath = fullFilePath,
            UploadedAt = DateTime.UtcNow
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
            await _documentIndexingService.IndexAsync(document, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);

            if (File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
            }

            throw;
        }

        return ToResponse(document);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetUserDocumentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(document => document.UserId == userId)
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => new DocumentResponse(
                document.Id,
                document.FileName,
                document.OriginalFileName,
                document.ContentType,
                document.FilePath,
                document.UploadedAt))
            .ToListAsync(cancellationToken);
    }

    private string GetUploadsPath()
    {
        return Path.IsPathRooted(_storageSettings.UploadsPath)
            ? _storageSettings.UploadsPath
            : Path.Combine(AppContext.BaseDirectory, _storageSettings.UploadsPath);
    }

    private static void ValidateUploadRequest(UploadDocumentRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var extension = Path.GetExtension(request.OriginalFileName);

        if (request.UserId == Guid.Empty)
        {
            errors[nameof(request.UserId)] = ["An authenticated user is required."];
        }

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            errors[nameof(request.OriginalFileName)] = ["File name is required."];
        }
        else if (!AllowedExtensions.Contains(extension))
        {
            errors[nameof(request.OriginalFileName)] = ["Only .pdf and .docx files are allowed."];
        }

        if (string.IsNullOrWhiteSpace(request.ContentType) || !AllowedContentTypes.Contains(request.ContentType))
        {
            errors[nameof(request.ContentType)] = ["Only PDF and DOCX content types are allowed."];
        }

        if (!request.Content.CanRead || request.FileSize <= 0)
        {
            errors[nameof(request.Content)] = ["A non-empty file is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static DocumentResponse ToResponse(Document document)
    {
        return new DocumentResponse(
            document.Id,
            document.FileName,
            document.OriginalFileName,
            document.ContentType,
            document.FilePath,
            document.UploadedAt);
    }
}
