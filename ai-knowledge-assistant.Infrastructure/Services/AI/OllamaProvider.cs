using ai_knowledge_assistant.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ai_knowledge_assistant.Infrastructure.Services.AI;

public sealed class OllamaProvider : LocalProviderBase
{
    public OllamaProvider(IOptions<AISettings> settings, ILogger<OllamaProvider> logger)
        : base(settings, logger)
    {
    }

    public override string Name => "Ollama";
}
