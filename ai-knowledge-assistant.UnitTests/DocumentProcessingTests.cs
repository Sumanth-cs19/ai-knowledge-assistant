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
using DocumentFormat.OpenXml.Packaging;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using Body = DocumentFormat.OpenXml.Wordprocessing.Body;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ai_knowledge_assistant.UnitTests;

public sealed class DocumentProcessingTests
{
    [Fact]
    public async Task Pdf_extraction_returns_readable_text()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            WriteMinimalPdf(path, "National Pension System mapping instructions");

            var text = await new TextExtractionService().ExtractTextAsync(path, "application/pdf", "sample.pdf");

            Assert.Contains("National Pension System", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Docx_extraction_returns_readable_text()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        try
        {
            using (var package = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = package.AddMainDocumentPart();
                mainPart.Document = new WordDocument(
                    new Body(new Paragraph(new Run(new Text("Readable DOCX knowledge content")))));
                mainPart.Document.Save();
            }

            var text = await new TextExtractionService().ExtractTextAsync(
                path,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "sample.docx");

            Assert.Contains("Readable DOCX knowledge content", text, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(path);
        }
    }

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

        var longText = string.Join(' ', Enumerable.Repeat("Alpha beta gamma delta forms one complete sentence.", 120));
        var service = new DocumentIndexingService(
            context,
            new FakeTextExtractionService(longText),
            new FakeEmbeddingProvider(),
            NullLogger<DocumentIndexingService>.Instance);

        await service.IndexAsync(document);

        var chunks = context.DocumentChunks
            .Where(chunk => chunk.DocumentId == document.Id)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToList();
        Assert.True(chunks.Count > 1);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.All(chunks.SkipLast(1), chunk => Assert.EndsWith(".", chunk.Content));
        Assert.All(chunks, chunk =>
        {
            Assert.NotNull(chunk.Embedding);
            Assert.Equal(1536, chunk.Embedding!.ToArray().Length);
        });
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

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Windows can briefly retain a PDF font/resource handle after iText closes the document.
        }
    }

    private static void WriteMinimalPdf(string path, string text)
    {
        var content = $"BT /F1 12 Tf 36 750 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };

        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{index + 1} 0 obj");
            pdf.AppendLine(objects[index]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Length + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        }

        pdf.AppendLine($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>");
        pdf.AppendLine($"startxref\n{xrefOffset}\n%%EOF");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
    }
}
