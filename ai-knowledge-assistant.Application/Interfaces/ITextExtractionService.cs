namespace ai_knowledge_assistant.Application.Interfaces;

public interface ITextExtractionService
{
    Task<string> ExtractTextAsync(
        string filePath,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default);
}
