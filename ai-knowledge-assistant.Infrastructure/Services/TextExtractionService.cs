using System.Text;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class TextExtractionService : ITextExtractionService
{
    public Task<string> ExtractTextAsync(
        string filePath,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);

        try
        {
            var text = extension.ToLowerInvariant() switch
            {
                ".pdf" => ExtractPdfText(filePath),
                ".docx" => ExtractDocxText(filePath),
                _ => throw UnsupportedFileType()
            };

            return Task.FromResult(text);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["document"] = [$"Unable to parse the uploaded document: {exception.Message}"]
            });
        }
    }

    private static string ExtractPdfText(string filePath)
    {
        var text = new StringBuilder();

        using var reader = new PdfReader(filePath);
        using var document = new PdfDocument(reader);

        for (var pageNumber = 1; pageNumber <= document.GetNumberOfPages(); pageNumber++)
        {
            text.AppendLine(PdfTextExtractor.GetTextFromPage(document.GetPage(pageNumber)));
        }

        return text.ToString();
    }

    private static string ExtractDocxText(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var mainDocumentPart = document.MainDocumentPart;
        return mainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }

    private static ValidationException UnsupportedFileType()
    {
        return new ValidationException(new Dictionary<string, string[]>
        {
            ["document"] = ["Only .pdf and .docx documents are supported for text extraction."]
        });
    }
}
