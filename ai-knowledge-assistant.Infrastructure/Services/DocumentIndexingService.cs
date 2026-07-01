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

        _logger.LogInformation(
            "Document text extracted. DocumentId={DocumentId}. FileName={FileName}. FileType={FileType}. ExtractedLength={ExtractedLength}",
            document.Id,
            document.OriginalFileName,
            Path.GetExtension(document.OriginalFileName),
            extractedText.Length);
        _logger.LogDebug(
            "Extracted text preview for {DocumentId}. First500={First500}. Last500={Last500}",
            document.Id,
            Preview(extractedText, fromEnd: false),
            Preview(extractedText, fromEnd: true));

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
            var chunk = new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = chunkIndex,
                Content = content,
                Embedding = new Vector(embedding),
                CreatedAt = DateTime.UtcNow
            };
            chunkEntities.Add(chunk);
            chunkIndex++;
        }

        _context.DocumentChunks.AddRange(chunkEntities);
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var chunk in chunkEntities)
        {
            _logger.LogInformation(
                "Document chunk embedded and stored. DocumentId={DocumentId}. ChunkId={ChunkId}. ChunkIndex={ChunkIndex}. CharacterCount={CharacterCount}. EmbeddingProvider={EmbeddingProvider}. EmbeddingDimension={EmbeddingDimension}. Stored={Stored}",
                document.Id,
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content.Length,
                _embeddingProvider.Name,
                chunk.Embedding?.ToArray().Length ?? 0,
                true);
        }
        activity?.SetTag("document.chunk_count", chunkEntities.Count);
        _logger.LogInformation(
            "Indexed document {DocumentId} with {ChunkCount} chunks. AverageChunkSize={AverageChunkSize}. ChunkOverlap={ChunkOverlap}. ExtractionQualityScore={QualityScore}",
            document.Id,
            chunkEntities.Count,
            chunkEntities.Count == 0 ? 0 : Math.Round(chunkEntities.Average(chunk => chunk.Content.Length), 1),
            ChunkOverlap,
            quality.Score);
        _logger.LogDebug(
            "Chunk previews for {DocumentId}. FirstChunks={FirstChunks}. LastChunks={LastChunks}",
            document.Id,
            chunkEntities.Take(3).Select(chunk => new { chunk.ChunkIndex, Preview = Preview(chunk.Content, false, 300) }).ToArray(),
            chunkEntities.TakeLast(3).Select(chunk => new { chunk.ChunkIndex, Preview = Preview(chunk.Content, false, 300) }).ToArray());
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
            var targetEnd = Math.Min(start + ChunkSize, text.Length);
            var end = targetEnd == text.Length ? targetEnd : FindNaturalBoundary(text, start, targetEnd);
            var chunk = text[start..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }

            if (end >= text.Length)
            {
                break;
            }

            var nextStart = Math.Max(start + 1, end - ChunkOverlap);
            while (nextStart < end && nextStart > 0 && !char.IsWhiteSpace(text[nextStart - 1]))
            {
                nextStart++;
            }

            start = nextStart;
        }
    }

    private static int FindNaturalBoundary(string text, int start, int targetEnd)
    {
        var minimumEnd = Math.Min(targetEnd, start + (int)(ChunkSize * 0.8));
        for (var index = targetEnd - 1; index >= minimumEnd; index--)
        {
            if (text[index] is '.' or '!' or '?' or '\n')
            {
                return index + 1;
            }
        }

        for (var index = targetEnd - 1; index >= minimumEnd; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index + 1;
            }
        }

        return targetEnd;
    }

    private static string Preview(string value, bool fromEnd, int length = 500)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length <= length)
        {
            return normalized;
        }

        return fromEnd ? normalized[^length..] : normalized[..length];
    }
}
