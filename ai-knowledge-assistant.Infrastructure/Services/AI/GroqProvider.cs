using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ai_knowledge_assistant.Application.Common;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services.AI;

public sealed class GroqProvider : LocalProviderBase
{
    private static readonly ActivitySource ActivitySource = new(Observability.ActivitySourceName);
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqProvider> _logger;
    private readonly AISettings _settings;

    public GroqProvider(
        HttpClient httpClient,
        IOptions<AISettings> settings,
        ILogger<GroqProvider> logger)
        : base(settings, logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public override string Name => "Groq";

    public override async Task<string> GenerateAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        if (contextChunks.Count == 0)
        {
            throw new AIProviderException("No document context was provided to the AI provider.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey)
            || string.IsNullOrWhiteSpace(_settings.Endpoint)
            || string.IsNullOrWhiteSpace(_settings.Model))
        {
            throw new AIProviderException("Groq configuration is incomplete.");
        }

        using var activity = ActivitySource.StartActivity("ai.groq.chat_completion");
        activity?.SetTag("ai.provider", Name);
        activity?.SetTag("ai.model", _settings.Model);
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Sending Groq RAG request. Model={Model}. ContextChunkCount={ContextChunkCount}. PromptLength={PromptLength}",
            _settings.Model,
            contextChunks.Count,
            prompt.Length);
        _logger.LogDebug("Groq RAG request prompt: {Prompt}", prompt);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = _settings.Model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "Answer only from the supplied document context. If the context is insufficient, say so clearly."
                    },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Groq request failed with status code {StatusCode} for model {Model}",
                    (int)response.StatusCode,
                    _settings.Model);
                throw new AIProviderException($"Groq returned HTTP {(int)response.StatusCode}.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var answer = payload.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            var totalTokens = payload.RootElement.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("total_tokens", out var totalTokensElement)
                ? totalTokensElement.GetInt32()
                : (int?)null;

            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new AIProviderException("Groq returned an empty response.");
            }

            stopwatch.Stop();
            activity?.SetTag("ai.duration_ms", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation(
                "Groq generated a response with model {Model}. DurationMs={DurationMs}. TokenUsage={TokenUsage}. ResponseLength={ResponseLength}",
                _settings.Model,
                stopwatch.ElapsedMilliseconds,
                totalTokens,
                answer.Length);
            _logger.LogDebug("Raw Groq RAG response: {Response}", answer);
            return answer;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AIProviderException("Groq request timed out.");
        }
        catch (AIProviderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Groq request failed for model {Model}", _settings.Model);
            throw new AIProviderException("Groq failed to generate a response.", exception);
        }
    }
}
