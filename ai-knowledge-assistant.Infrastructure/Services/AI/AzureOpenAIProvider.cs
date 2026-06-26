using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services.AI;

public sealed class AzureOpenAIProvider : LocalProviderBase
{
    public AzureOpenAIProvider(IOptions<AISettings> settings, ILogger<AzureOpenAIProvider> logger)
        : base(settings, logger)
    {
    }

    public override string Name => "AzureOpenAI";
}
