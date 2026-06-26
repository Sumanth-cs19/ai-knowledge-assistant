using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Api.Health;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly StorageSettings _settings;

    public StorageHealthCheck(IOptions<StorageSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var uploadPath = Path.IsPathRooted(_settings.UploadsPath)
                ? _settings.UploadsPath
                : Path.Combine(AppContext.BaseDirectory, _settings.UploadsPath);

            Directory.CreateDirectory(uploadPath);
            var probeFile = Path.Combine(uploadPath, $".health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);

            return Task.FromResult(HealthCheckResult.Healthy("Upload storage is writable."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Upload storage is not writable.", exception));
        }
    }
}
