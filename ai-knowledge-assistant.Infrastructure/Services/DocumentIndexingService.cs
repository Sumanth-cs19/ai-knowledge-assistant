using System.Diagnostics;
using System.Text.RegularExpressions;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private const int ChunkSize = 1000;
    private const int ChunkOverlap = 150;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<DocumentIndexingService> _logger;
    private readonly ITextExtractionService _textExtractionService;

    public DocumentIndexingService(
        ApplicationDbContext context,
        ITextExtractionService textExtractionService,
        IEmbeddingProvider embeddingProvider,
        ILogger<DocumentIndexingService> logger)
    {
        _context = context;
        _textExtractionService = textExtractionService;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    public async Task IndexAsync(Document document, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("document.index");
        activity?.SetTag("document.id", document.Id);
        activity?.SetTag("document.file_name", document.OriginalFileName);
        _logger.LogInformation("Indexing document {DocumentId}", document.Id);
        var extractedText = await _textExtractionService.ExtractTextAsync(
            document.FilePath,
            document.ContentType,
            document.OriginalFileName,
            cancellationToken);

        var quality = TextQualityAnalyzer.Analyze(extractedText);
        activity?.SetTag("document.extraction_quality_score", quality.Score);
        _logger.LogInformation(
            "Document extraction quality evaluated for {DocumentId}. QualityScore={QualityScore}. CharacterCount={CharacterCount}. WordCount={WordCount}. IsLowQuality={IsLowQuality}",
            document.Id,
            quality.Score,
            quality.CharacterCount,
            quality.WordCount,
            quality.IsLowQuality);

        if (quality.IsLowQuality)
        {
            throw new InvalidDataException(TextQualityAnalyzer.LowQualityMessage);
        }

        var normalizedText = NormalizeText(extractedText);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new InvalidDataException(TextQualityAnalyzer.LowQualityMessage);
        }

        var existingChunks = _context.DocumentChunks.Where(chunk => chunk.DocumentId == document.Id);
        _context.DocumentChunks.RemoveRange(existingChunks);

        var chunkEntities = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var content in CreateChunks(normalizedText))
        {
            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(content, cancellationToken);
            chunkEntities.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = chunkIndex,
                Content = content,
                Embedding = new Vector(embedding),
                CreatedAt = DateTime.UtcNow
            });
            chunkIndex++;
        }

        _context.DocumentChunks.AddRange(chunkEntities);
        await _context.SaveChangesAsync(cancellationToken);
        activity?.SetTag("document.chunk_count", chunkEntities.Count);
        _logger.LogInformation(
            "Indexed document {DocumentId} with {ChunkCount} chunks. ExtractionQualityScore={QualityScore}",
            document.Id,
            chunkEntities.Count,
            quality.Score);
    }

    private static string NormalizeText(string text)
    {
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static IEnumerable<string> CreateChunks(string text)
    {
        if (text.Length <= ChunkSize)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(ChunkSize, text.Length - start);
            var chunk = text.Substring(start, length).Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }

            if (start + length >= text.Length)
            {
                break;
            }

            start += ChunkSize - ChunkOverlap;
        }
    }
}
