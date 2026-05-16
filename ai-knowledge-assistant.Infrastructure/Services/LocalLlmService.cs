using System.Text;
using System.Runtime.CompilerServices;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class LocalLlmService : ILlmService
{
    public Task<string> GenerateAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        if (contextChunks.Count == 0)
        {
            throw new InvalidOperationException("No document context was provided to the language model.");
        }

        var answer = new StringBuilder();
        answer.AppendLine("Based on the uploaded document context:");
        answer.AppendLine();

        foreach (var chunk in contextChunks.Take(3))
        {
            answer.AppendLine(SummarizeChunk(chunk));
        }

        answer.AppendLine();
        answer.AppendLine("This answer is grounded only in the retrieved document excerpts. Connect an external LLM provider later to generate more fluent synthesis from the same prompt.");

        return Task.FromResult(answer.ToString().Trim());
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var answer = await GenerateAnswerAsync(prompt, contextChunks, cancellationToken);
        var tokens = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < tokens.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return index == tokens.Length - 1 ? tokens[index] : $"{tokens[index]} ";
            await Task.Delay(20, cancellationToken);
        }
    }

    private static string SummarizeChunk(string chunk)
    {
        const int maxLength = 450;
        var trimmed = chunk.Trim();

        if (trimmed.Length <= maxLength)
        {
            return $"- {trimmed}";
        }

        return $"- {trimmed[..maxLength].Trim()}...";
    }
}
