namespace ai_knowledge_assistant.Application.Interfaces;

public interface ILlmService
{
    Task<string> GenerateAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default);
}
