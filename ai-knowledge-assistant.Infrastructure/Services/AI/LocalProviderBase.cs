using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services.AI;

public abstract class LocalProviderBase : IAIProvider, IEmbeddingProvider
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private readonly ILogger _logger;
    private readonly AISettings _settings;

    protected LocalProviderBase(IOptions<AISettings> settings, ILogger logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public abstract string Name { get; }

    public int Dimensions => 1536;

    public virtual Task<string> GenerateAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ai.generate_answer");
        activity?.SetTag("ai.provider", Name);
        activity?.SetTag("ai.model", _settings.Model);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (contextChunks.Count == 0)
            {
                throw new AIProviderException("No document context was provided to the AI provider.");
            }

            var answer = BuildGroundedAnswer(contextChunks);
            stopwatch.Stop();
            activity?.SetTag("ai.estimated_tokens", EstimateTokenCount(prompt) + EstimateTokenCount(answer));
            activity?.SetTag("ai.duration_ms", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation(
                "AI provider {Provider} generated response with model {Model}. EstimatedTokens={TokenCount}. DurationMs={DurationMs}",
                Name,
                _settings.Model,
                EstimateTokenCount(prompt) + EstimateTokenCount(answer),
                stopwatch.ElapsedMilliseconds);

            return Task.FromResult(answer);
        }
        catch (AIProviderException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "AI provider {Provider} failed while generating an answer", Name);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw new AIProviderException($"{Name} failed to generate a response.", exception);
        }
    }

    public virtual async IAsyncEnumerable<string> StreamAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var answer = await GenerateAnswerAsync(prompt, contextChunks, cancellationToken);
        var tokens = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return $"{token} ";
            await Task.Delay(20, cancellationToken);
        }
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ai.generate_embedding");
        activity?.SetTag("ai.provider", Name);
        activity?.SetTag("ai.embedding_model", _settings.EmbeddingModel);
        var stopwatch = Stopwatch.StartNew();
        var embedding = new float[Dimensions];
        var tokens = Regex
            .Matches(text.ToLowerInvariant(), @"[a-z0-9]+")
            .Select(match => match.Value);

        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var dimension = BitConverter.ToUInt32(hash, 0) % Dimensions;
            var sign = (hash[4] & 1) == 0 ? 1f : -1f;
            embedding[dimension] += sign;
        }

        Normalize(embedding);
        stopwatch.Stop();
        activity?.SetTag("ai.estimated_tokens", EstimateTokenCount(text));
        activity?.SetTag("ai.duration_ms", stopwatch.ElapsedMilliseconds);
        _logger.LogInformation(
            "AI provider {Provider} generated embedding with model {EmbeddingModel}. EstimatedTokens={TokenCount}. DurationMs={DurationMs}",
            Name,
            _settings.EmbeddingModel,
            EstimateTokenCount(text),
            stopwatch.ElapsedMilliseconds);

        return Task.FromResult(embedding);
    }

    private static string BuildGroundedAnswer(IReadOnlyCollection<string> contextChunks)
    {
        var answer = new StringBuilder();
        answer.AppendLine("Based on the uploaded document context:");
        answer.AppendLine();

        foreach (var chunk in contextChunks.Take(3))
        {
            answer.AppendLine(SummarizeChunk(chunk));
        }

        answer.AppendLine();
        answer.AppendLine("This answer is grounded only in the retrieved document excerpts.");

        return answer.ToString().Trim();
    }

    private static string SummarizeChunk(string chunk)
    {
        const int maxLength = 450;
        var trimmed = chunk.Trim();

        return trimmed.Length <= maxLength
            ? $"- {trimmed}"
            : $"- {trimmed[..maxLength].Trim()}...";
    }

    private static void Normalize(float[] embedding)
    {
        var magnitude = Math.Sqrt(embedding.Sum(value => value * value));
        if (magnitude == 0)
        {
            return;
        }

        for (var index = 0; index < embedding.Length; index++)
        {
            embedding[index] = (float)(embedding[index] / magnitude);
        }
    }

    private static int EstimateTokenCount(string content)
    {
        return string.IsNullOrWhiteSpace(content)
            ? 0
            : Math.Max(1, (int)Math.Ceiling(content.Length / 4.0));
    }
}
