using System.Text.RegularExpressions;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Persistence;
using Pgvector;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private const int ChunkSize = 1000;
    private const int ChunkOverlap = 150;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ITextExtractionService _textExtractionService;

    public DocumentIndexingService(
        ApplicationDbContext context,
        ITextExtractionService textExtractionService,
        IEmbeddingGenerator embeddingGenerator)
    {
        _context = context;
        _textExtractionService = textExtractionService;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task IndexAsync(Document document, CancellationToken cancellationToken = default)
    {
        var extractedText = await _textExtractionService.ExtractTextAsync(
            document.FilePath,
            document.ContentType,
            document.OriginalFileName,
            cancellationToken);

        var normalizedText = NormalizeText(extractedText);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["document"] = ["No extractable text was found in the uploaded document."]
            });
        }

        var chunks = CreateChunks(normalizedText)
            .Select((content, index) => new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = index,
                Content = content,
                Embedding = new Vector(_embeddingGenerator.GenerateEmbedding(content)),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        _context.DocumentChunks.AddRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
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
