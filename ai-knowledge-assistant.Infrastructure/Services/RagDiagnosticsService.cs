using ai_knowledge_assistant.Application.DTOs.Admin;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Features.Chat;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class RagDiagnosticsService : IRagDiagnosticsService
{
    private const int DiagnosticRetrievalCount = 10;
    private readonly IAIProvider _aiProvider;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<RagDiagnosticsService> _logger;
    private readonly ISemanticSearchService _searchService;

    public RagDiagnosticsService(
        ApplicationDbContext context,
        ISemanticSearchService searchService,
        IAIProvider aiProvider,
        IEmbeddingProvider embeddingProvider,
        ILogger<RagDiagnosticsService> logger)
    {
        _context = context;
        _searchService = searchService;
        _aiProvider = aiProvider;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<RagDocumentDiagnosticResponse>> GetDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Include(document => document.Chunks)
            .OrderByDescending(document => document.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(ToSummary).ToList();
    }

    public async Task<RagDocumentDetailResponse> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .Include(item => item.Chunks)
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        var chunks = document.Chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new RagChunkDiagnosticResponse(
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content.Length,
                chunk.Content,
                GetDimension(chunk),
                chunk.Embedding is not null))
            .ToList();
        var reconstructedText = string.Join(Environment.NewLine, chunks.Select(chunk => chunk.Content));

        return new RagDocumentDetailResponse(
            ToSummary(document),
            reconstructedText.Length <= 1500 ? reconstructedText : reconstructedText[..1500],
            chunks);
    }

    public async Task<RagTestResponse> TestAsync(
        RagTestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DocumentId == Guid.Empty || string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.DocumentId)] = ["A document is required."],
                [nameof(request.Question)] = ["A test question is required."]
            });
        }

        var document = await _context.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");
        var broadContextMode = RagPromptBuilder.IsBroadContextQuestion(request.Question);
        IReadOnlyCollection<SearchResultResponse> results = broadContextMode
            ? await _searchService.GetDocumentContextAsync(
                document.UserId,
                [document.Id],
                DiagnosticRetrievalCount,
                cancellationToken)
            : await _searchService.SearchAsync(
                document.UserId,
                new SearchQueryRequest(request.Question, DiagnosticRetrievalCount, [document.Id]),
                cancellationToken);
        var prompt = RagPromptBuilder.Build(request.Question, results, broadContextMode);
        var rawResponse = results.Count == 0
            ? RagPromptBuilder.NoContextAnswer
            : await _aiProvider.GenerateAnswerAsync(
                prompt,
                results.Select(result => result.Content).ToList(),
                cancellationToken);

        _logger.LogInformation(
            "Admin RAG diagnostic test completed. DocumentId={DocumentId}. BroadContextMode={BroadContextMode}. ResultCount={ResultCount}. Scores={Scores}. Provider={Provider}",
            document.Id,
            broadContextMode,
            results.Count,
            results.Select(result => Math.Round(result.Similarity, 6)).ToArray(),
            _aiProvider.Name);

        return new RagTestResponse(
            document.Id,
            request.Question.Trim(),
            broadContextMode,
            results,
            prompt,
            rawResponse);
    }

    private RagDocumentDiagnosticResponse ToSummary(Document document)
    {
        var orderedChunks = document.Chunks.OrderBy(chunk => chunk.ChunkIndex).ToList();
        var reconstructedText = string.Join(' ', orderedChunks.Select(chunk => chunk.Content));
        var quality = TextQualityAnalyzer.Analyze(reconstructedText);
        var storedEmbeddingCount = orderedChunks.Count(chunk => chunk.Embedding is not null);
        var embeddingDimension = orderedChunks
            .Select(GetDimension)
            .FirstOrDefault(dimension => dimension > 0);
        var vectorStatus = orderedChunks.Count == 0
            ? "No chunks"
            : storedEmbeddingCount == orderedChunks.Count
                ? "Complete"
                : $"Missing {orderedChunks.Count - storedEmbeddingCount}";

        return new RagDocumentDiagnosticResponse(
            document.Id,
            document.OriginalFileName,
            document.FileName,
            document.UploadedAt,
            document.Status,
            document.VersionNumber,
            quality.Score,
            quality.IsLowQuality ? "Low" : quality.Score >= 0.8 ? "Good" : "Fair",
            reconstructedText.Length,
            orderedChunks.Count,
            orderedChunks.Count == 0 ? 0 : Math.Round(orderedChunks.Average(chunk => chunk.Content.Length), 1),
            _embeddingProvider.Name,
            embeddingDimension == 0 ? _embeddingProvider.Dimensions : embeddingDimension,
            storedEmbeddingCount,
            vectorStatus,
            document.ErrorMessage ?? (quality.IsLowQuality ? TextQualityAnalyzer.LowQualityMessage : null));
    }

    private static int GetDimension(DocumentChunk chunk)
    {
        return chunk.Embedding?.ToArray().Length ?? 0;
    }
}
