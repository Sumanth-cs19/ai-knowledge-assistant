using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.Application.DTOs.Common;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IDocumentProcessingQueue _documentProcessingQueue;
    private readonly ILogger<DocumentService> _logger;
    private readonly StorageSettings _storageSettings;

    public DocumentService(
        ApplicationDbContext context,
        IDocumentProcessingQueue documentProcessingQueue,
        ILogger<DocumentService> logger,
        IOptions<StorageSettings> storageSettings)
    {
        _context = context;
        _documentProcessingQueue = documentProcessingQueue;
        _logger = logger;
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
            UploadedAt = DateTime.UtcNow,
            Status = DocumentStatus.Pending,
            VersionNumber = await GetNextVersionNumberAsync(request.UserId, safeOriginalFileName, cancellationToken)
        };

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
            await _documentProcessingQueue.QueueAsync(document.Id, cancellationToken);
            _logger.LogInformation(
                "Document uploaded by user {UserId}. DocumentId={DocumentId}. OriginalFileName={OriginalFileName}. Version={VersionNumber}",
                request.UserId,
                document.Id,
                document.OriginalFileName,
                document.VersionNumber);
        }
        catch
        {
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
            .Where(document => document.UserId == userId && !document.IsDeleted)
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => ToResponse(document))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<DocumentResponse>> GetUserDocumentsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        var query = _context.Documents
            .AsNoTracking()
            .Where(document => document.UserId == userId && !document.IsDeleted)
            .OrderByDescending(document => document.UploadedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(document => ToResponse(document))
            .ToListAsync(cancellationToken);

        return new PagedResponse<DocumentResponse>(items, safePage, safePageSize, total);
    }

    public async Task<DocumentResponse> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await GetOwnedDocumentAsync(userId, id, cancellationToken);
        return ToResponse(document);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await GetOwnedDocumentAsync(userId, id, cancellationToken);
        document.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReindexAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await GetOwnedDocumentAsync(userId, id, cancellationToken);
        document.Status = DocumentStatus.Pending;
        document.ErrorMessage = null;
        document.ProcessedAt = null;
        await _context.SaveChangesAsync(cancellationToken);
        await _documentProcessingQueue.QueueAsync(document.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetVersionsAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await GetOwnedDocumentAsync(userId, id, cancellationToken);
        return await _context.Documents
            .AsNoTracking()
            .Where(version => version.UserId == userId
                && version.OriginalFileName == document.OriginalFileName
                && !version.IsDeleted)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => ToResponse(version))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<DocumentChunkResponse>> GetChunksAsync(
        Guid userId,
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _ = await GetOwnedDocumentAsync(userId, id, cancellationToken);
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
        var query = _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == id)
            .OrderBy(chunk => chunk.ChunkIndex);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(chunk => new DocumentChunkResponse(
                chunk.Id,
                chunk.DocumentId,
                chunk.ChunkIndex,
                chunk.Content,
                chunk.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<DocumentChunkResponse>(items, safePage, safePageSize, total);
    }

    private string GetUploadsPath()
    {
        return Path.IsPathRooted(_storageSettings.UploadsPath)
            ? _storageSettings.UploadsPath
            : Path.Combine(AppContext.BaseDirectory, _storageSettings.UploadsPath);
    }

    private async Task<int> GetNextVersionNumberAsync(
        Guid userId,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var currentMaxVersion = await _context.Documents
            .Where(document => document.UserId == userId && document.OriginalFileName == originalFileName)
            .Select(document => (int?)document.VersionNumber)
            .MaxAsync(cancellationToken);

        return (currentMaxVersion ?? 0) + 1;
    }

    private async Task<Document> GetOwnedDocumentAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(document => document.Id == id && document.UserId == userId && !document.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");
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
            document.UploadedAt,
            document.Status,
            document.ErrorMessage,
            document.ProcessedAt,
            document.VersionNumber,
            document.IsDeleted);
    }
}
