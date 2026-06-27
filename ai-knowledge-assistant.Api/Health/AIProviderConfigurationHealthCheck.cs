using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Api.Health;

public sealed class AIProviderConfigurationHealthCheck : IHealthCheck
{
    private readonly AISettings _settings;

    public AIProviderConfigurationHealthCheck(IOptions<AISettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Provider)
            || string.IsNullOrWhiteSpace(_settings.Model)
            || string.IsNullOrWhiteSpace(_settings.EmbeddingModel))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "AI provider, model, and embedding model must be configured."));
        }

        if (_settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.Endpoint)))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Groq API key and endpoint must be configured."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("AI provider configuration is present."));
    }
}
