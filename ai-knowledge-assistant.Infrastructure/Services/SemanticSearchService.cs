using System.Diagnostics;
using System.Text.RegularExpressions;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class SemanticSearchService : ISemanticSearchService
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "could", "document", "from", "have", "please", "should", "that", "the", "this", "what", "when", "where", "which", "with", "would"
    };
    private const int DefaultTopK = 5;
    private const int MaxTopK = 20;
    private const double MinimumRelevantScore = 0.08;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly string _scoreType;

    public SemanticSearchService(
        ApplicationDbContext context,
        IEmbeddingProvider embeddingProvider,
        IOptions<AISettings> settings,
        ILogger<SemanticSearchService> logger)
    {
        _context = context;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
        _scoreType = settings.Value.EmbeddingModel.Equals("local-hash-embedding", StringComparison.OrdinalIgnoreCase)
            ? "local-fallback"
            : "hybrid";
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
        var queryTerms = Tokenize(request.Query)
            .Where(term => term.Length > 2 && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedDocumentIds = request.DocumentIds?
            .Where(documentId => documentId != Guid.Empty)
            .Distinct()
            .ToArray();

        var candidateQuery = _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Document != null
                && chunk.Document.UserId == userId
                && !chunk.Document.IsDeleted
                && chunk.Document.Status == DocumentStatus.Indexed
                && chunk.Embedding != null);

        if (selectedDocumentIds is { Length: > 0 })
        {
            candidateQuery = candidateQuery.Where(chunk => selectedDocumentIds.Contains(chunk.DocumentId));
        }

        var candidates = await candidateQuery
            .OrderBy(chunk => chunk.Embedding!.CosineDistance(queryEmbedding))
            .Take(Math.Max(topK * 8, topK))
            .Select(chunk => new SearchResultResponse(
                chunk.DocumentId,
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content,
                1 - chunk.Embedding!.CosineDistance(queryEmbedding),
                chunk.Document!.FileName,
                chunk.Document.OriginalFileName,
                chunk.Document.UploadedAt,
                _scoreType))
            .ToListAsync(cancellationToken);

        var results = candidates
            .Select(result =>
            {
                var keywordScore = CalculateKeywordScore(queryTerms, result.Content);
                var vectorScore = Math.Clamp(result.Similarity, 0, 1);
                var combinedScore = CalculateCombinedScore(vectorScore, keywordScore);
                return result with { Similarity = combinedScore };
            })
            .Where(result => result.Similarity >= MinimumRelevantScore)
            .OrderByDescending(result => result.Similarity)
            .Take(topK)
            .ToList();

        stopwatch.Stop();
        activity?.SetTag("search.result_count", results.Count);
        activity?.SetTag("search.duration_ms", stopwatch.ElapsedMilliseconds);
        _logger.LogInformation(
            "Semantic hybrid search completed for user {UserId}. Results={ResultCount}. DurationMs={DurationMs}. ScoreType={ScoreType}. SelectedChunks={SelectedChunks}. SimilarityScores={SimilarityScores}",
            userId,
            results.Count,
            stopwatch.ElapsedMilliseconds,
            _scoreType,
            results.Select(result => result.ChunkId).ToArray(),
            results.Select(result => Math.Round(result.Similarity, 4)).ToArray());

        return results;
    }

    public async Task<IReadOnlyCollection<SearchResultResponse>> GetDocumentContextAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? documentIds,
        int maxChunks,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(userId)] = ["An authenticated user is required."]
            });
        }

        var safeMaxChunks = Math.Clamp(maxChunks, 1, MaxTopK);
        var requestedDocumentIds = documentIds?
            .Where(documentId => documentId != Guid.Empty)
            .Distinct()
            .ToArray();
        var documentQuery = _context.Documents
            .AsNoTracking()
            .Where(document => document.UserId == userId
                && !document.IsDeleted
                && document.Status == DocumentStatus.Indexed);

        if (requestedDocumentIds is { Length: > 0 })
        {
            documentQuery = documentQuery.Where(document => requestedDocumentIds.Contains(document.Id));
        }

        var targetDocumentIds = requestedDocumentIds is { Length: > 0 }
            ? await documentQuery
                .OrderByDescending(document => document.UploadedAt)
                .Select(document => document.Id)
                .ToArrayAsync(cancellationToken)
            : await documentQuery
                .OrderByDescending(document => document.UploadedAt)
                .Select(document => document.Id)
                .Take(1)
                .ToArrayAsync(cancellationToken);

        if (targetDocumentIds.Length == 0)
        {
            return [];
        }

        var results = await _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => targetDocumentIds.Contains(chunk.DocumentId) && chunk.Document != null)
            .OrderByDescending(chunk => chunk.Document!.UploadedAt)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Take(safeMaxChunks)
            .Select(chunk => new SearchResultResponse(
                chunk.DocumentId,
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content,
                1,
                chunk.Document!.FileName,
                chunk.Document.OriginalFileName,
                chunk.Document.UploadedAt,
                "document-coverage"))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Selected ordered document context for summarization. UserId={UserId}. DocumentIds={DocumentIds}. ChunkCount={ChunkCount}. SelectedChunks={SelectedChunks}",
            userId,
            targetDocumentIds,
            results.Count,
            results.Select(result => result.ChunkId).ToArray());

        return results;
    }

    public static double CalculateCombinedScore(double vectorSimilarity, double keywordScore)
    {
        return Math.Clamp((vectorSimilarity * 0.75) + (keywordScore * 0.25), 0, 1);
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

    private static double CalculateKeywordScore(IReadOnlyCollection<string> queryTerms, string content)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var contentTerms = Tokenize(content).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return queryTerms.Count(contentTerms.Contains) / (double)queryTerms.Count;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return Regex
            .Matches(value.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value);
    }
}
