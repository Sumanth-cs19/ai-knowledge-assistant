using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private const int DefaultTopK = 5;
    private const int MaxTopK = 20;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingGenerator _embeddingGenerator;

    public SemanticSearchService(ApplicationDbContext context, IEmbeddingGenerator embeddingGenerator)
    {
        _context = context;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<IReadOnlyCollection<SearchResultResponse>> SearchAsync(
        Guid userId,
        SearchQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(userId, request);

        var topK = request.TopK <= 0 ? DefaultTopK : Math.Min(request.TopK, MaxTopK);
        var queryEmbedding = new Vector(_embeddingGenerator.GenerateEmbedding(request.Query));

        return await _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Document != null
                && chunk.Document.UserId == userId
                && chunk.Embedding != null)
            .OrderBy(chunk => chunk.Embedding!.CosineDistance(queryEmbedding))
            .Take(topK)
            .Select(chunk => new SearchResultResponse(
                chunk.DocumentId,
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content,
                1 - chunk.Embedding!.CosineDistance(queryEmbedding),
                chunk.Document!.FileName,
                chunk.Document.OriginalFileName,
                chunk.Document.UploadedAt))
            .ToListAsync(cancellationToken);
    }

    private static void ValidateRequest(Guid userId, SearchQueryRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (userId == Guid.Empty)
        {
            errors[nameof(userId)] = ["An authenticated user is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            errors[nameof(request.Query)] = ["Search query is required."];
        }

        if (request.TopK < 0 || request.TopK > MaxTopK)
        {
            errors[nameof(request.TopK)] = [$"TopK must be between 1 and {MaxTopK}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
