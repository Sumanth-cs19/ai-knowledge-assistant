using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.Infrastructure.Services;
using ai_knowledge_assistant.Infrastructure.Services.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
            }));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
        services.Configure<AISettings>(configuration.GetSection(AISettings.SectionName));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ITextExtractionService, TextExtractionService>();
        services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IChatFeedbackRepository, ChatFeedbackRepository>();
        services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();
        services.AddHostedService<DocumentProcessingWorker>();
        services.AddScoped<OpenAIProvider>();
        services.AddScoped<AzureOpenAIProvider>();
        services.AddScoped<GroqProvider>();
        services.AddScoped<OllamaProvider>();
        services.AddScoped<IAIProvider>(ResolveAIProvider);
        services.AddScoped<IEmbeddingProvider>(provider => (IEmbeddingProvider)ResolveAIProvider(provider));

        return services;
    }

    private static IAIProvider ResolveAIProvider(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IOptions<AISettings>>().Value;
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AIProviderResolver");

        IAIProvider provider = settings.Provider.Trim().ToLowerInvariant() switch
        {
            "openai" => serviceProvider.GetRequiredService<OpenAIProvider>(),
            "azureopenai" or "azure-openai" or "azure" => serviceProvider.GetRequiredService<AzureOpenAIProvider>(),
            "groq" => serviceProvider.GetRequiredService<GroqProvider>(),
            "ollama" or "local" => serviceProvider.GetRequiredService<OllamaProvider>(),
            _ => serviceProvider.GetRequiredService<OllamaProvider>()
        };

        logger.LogInformation(
            "Selected AI provider {Provider} with model {Model} and embedding model {EmbeddingModel}",
            provider.Name,
            settings.Model,
            settings.EmbeddingModel);

        return provider;
    }
}
