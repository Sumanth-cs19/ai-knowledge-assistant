using System.Diagnostics;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private const int DefaultTopK = 5;
    private const int MaxTopK = 20;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        ApplicationDbContext context,
        IEmbeddingProvider embeddingProvider,
        ILogger<SemanticSearchService> logger)
    {
        _context = context;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SearchResultResponse>> SearchAsync(
        Guid userId,
        SearchQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(userId, request);

        using var activity = ActivitySource.StartActivity("search.semantic");
        activity?.SetTag("user.id", userId);
        activity?.SetTag("search.top_k", request.TopK);
        var topK = request.TopK <= 0 ? DefaultTopK : Math.Min(request.TopK, MaxTopK);
        var stopwatch = Stopwatch.StartNew();
        var queryEmbedding = new Vector(await _embeddingProvider.GenerateEmbeddingAsync(request.Query, cancellationToken));
        var queryTerms = request.Query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var candidates = await _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Document != null
                && chunk.Document.UserId == userId
                && !chunk.Document.IsDeleted
                && chunk.Document.Status == DocumentStatus.Indexed
                && chunk.Embedding != null)
            .OrderBy(chunk => chunk.Embedding!.CosineDistance(queryEmbedding))
            .Take(Math.Max(topK * 4, topK))
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

        var results = candidates
            .Select(result =>
            {
                var keywordScore = queryTerms.Count == 0
                    ? 0
                    : queryTerms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase)) / (double)queryTerms.Count;
                var combinedScore = CalculateCombinedScore(result.Similarity, keywordScore);
                return result with { Similarity = combinedScore };
            })
            .OrderByDescending(result => result.Similarity)
            .Take(topK)
            .ToList();

        stopwatch.Stop();
        activity?.SetTag("search.result_count", results.Count);
        activity?.SetTag("search.duration_ms", stopwatch.ElapsedMilliseconds);
        _logger.LogInformation(
            "Semantic hybrid search completed for user {UserId}. Results={ResultCount}. DurationMs={DurationMs}",
            userId,
            results.Count,
            stopwatch.ElapsedMilliseconds);

        return results;
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

    public static double CalculateCombinedScore(double vectorSimilarity, double keywordScore)
    {
        return (vectorSimilarity * 0.75) + (keywordScore * 0.25);
    }
}
