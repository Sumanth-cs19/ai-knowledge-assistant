using System.Text;
using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Services;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.UnitTests;

public sealed class DocumentProcessingTests
{
    [Fact]
    public async Task Upload_rejects_unsupported_file_extension()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new DocumentService(
            context,
            new NoOpDocumentProcessingQueue(),
            NullLogger<DocumentService>.Instance,
            Options.Create(new StorageSettings { UploadsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }));

        var request = new UploadDocumentRequest(
            Guid.NewGuid(),
            "notes.txt",
            "text/plain",
            3,
            new MemoryStream("bad"u8.ToArray()));

        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(request));
    }

    [Fact]
    public async Task Indexing_splits_large_text_into_overlapping_chunks()
    {
        await using var context = TestDbContextFactory.Create();
        var user = new User { Email = "user@example.com", PasswordHash = "hash", RoleId = DefaultRoles.UserRoleId };
        var document = new Document
        {
            UserId = user.Id,
            FileName = "stored.pdf",
            OriginalFileName = "alpha.pdf",
            ContentType = "application/pdf",
            FilePath = "alpha.pdf"
        };
        context.Users.Add(user);
        context.Documents.Add(document);
        await context.SaveChangesAsync();

        var longText = string.Join(' ', Enumerable.Repeat("alpha beta gamma delta", 120));
        var service = new DocumentIndexingService(
            context,
            new FakeTextExtractionService(longText),
            new FakeEmbeddingProvider(),
            NullLogger<DocumentIndexingService>.Instance);

        await service.IndexAsync(document);

        Assert.True(context.DocumentChunks.Count(chunk => chunk.DocumentId == document.Id) > 1);
        Assert.Equal(0, context.DocumentChunks.OrderBy(chunk => chunk.ChunkIndex).First().ChunkIndex);
    }

    [Fact]
    public async Task Indexing_rejects_low_quality_extracted_text_with_ocr_guidance()
    {
        await using var context = TestDbContextFactory.Create();
        var user = new User { Email = "quality@example.com", PasswordHash = "hash", RoleId = DefaultRoles.UserRoleId };
        var document = new Document
        {
            UserId = user.Id,
            FileName = "noisy.pdf",
            OriginalFileName = "noisy.pdf",
            ContentType = "application/pdf",
            FilePath = "noisy.pdf"
        };
        context.Users.Add(user);
        context.Documents.Add(document);
        await context.SaveChangesAsync();

        var noisyText = string.Join(' ', Enumerable.Repeat("@@@ ### %%% | x ?", 80));
        var quality = TextQualityAnalyzer.Analyze(noisyText);
        var service = new DocumentIndexingService(
            context,
            new FakeTextExtractionService(noisyText),
            new FakeEmbeddingProvider(),
            NullLogger<DocumentIndexingService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.IndexAsync(document));

        Assert.True(quality.IsLowQuality);
        Assert.Equal(TextQualityAnalyzer.LowQualityMessage, exception.Message);
        Assert.Empty(context.DocumentChunks.Where(chunk => chunk.DocumentId == document.Id));
    }

    private sealed class NoOpDocumentProcessingQueue : IDocumentProcessingQueue
    {
        public ValueTask QueueAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Guid.Empty);
        }
    }
}
