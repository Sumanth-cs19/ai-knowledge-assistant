using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ai_knowledge_assistant.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var apiSettingsPath = ResolveApiSettingsPath();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiSettingsPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configuredConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is required for EF Core design-time operations.");
        }

        var connectionString = PostgreSqlConnectionString.Normalize(configuredConnectionString);

        Console.WriteLine($"EF design-time configuration base path: {apiSettingsPath}");
        Console.WriteLine($"EF design-time environment: {environment}");
        Console.WriteLine($"EF design-time DefaultConnection source: {GetConnectionStringSource(apiSettingsPath, environment)}");
        Console.WriteLine($"EF design-time DefaultConnection: {MaskPassword(connectionString)}");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.UseVector();
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiSettingsPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(currentDirectory, "ai-knowledge-assistant.Api"),
            currentDirectory,
            Path.Combine(currentDirectory, "..", "ai-knowledge-assistant.Api"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ai-knowledge-assistant.Api")
        };

        foreach (var candidate in candidates.Select(Path.GetFullPath))
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate ai-knowledge-assistant.Api appsettings.json from '{currentDirectory}'.");
    }

    private static string GetConnectionStringSource(string apiSettingsPath, string environment)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
        {
            return "environment variable ConnectionStrings__DefaultConnection";
        }

        var environmentSettingsPath = Path.Combine(apiSettingsPath, $"appsettings.{environment}.json");
        if (File.Exists(environmentSettingsPath)
            && File.ReadAllText(environmentSettingsPath).Contains("\"DefaultConnection\"", StringComparison.OrdinalIgnoreCase))
        {
            return environmentSettingsPath;
        }

        return Path.Combine(apiSettingsPath, "appsettings.json");
    }

    private static string MaskPassword(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
            {
                parts[index] = "Password=***";
            }
        }

        return string.Join(';', parts);
    }
}
