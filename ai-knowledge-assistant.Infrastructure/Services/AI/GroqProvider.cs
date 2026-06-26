using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services.AI;

public sealed class GroqProvider : LocalProviderBase
{
    public GroqProvider(IOptions<AISettings> settings, ILogger<GroqProvider> logger)
        : base(settings, logger)
    {
    }

    public override string Name => "Groq";
}
