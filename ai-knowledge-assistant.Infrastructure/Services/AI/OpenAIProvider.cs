using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services.AI;

public sealed class OpenAIProvider : LocalProviderBase
{
    public OpenAIProvider(IOptions<AISettings> settings, ILogger<OpenAIProvider> logger)
        : base(settings, logger)
    {
    }

    public override string Name => "OpenAI";
}
