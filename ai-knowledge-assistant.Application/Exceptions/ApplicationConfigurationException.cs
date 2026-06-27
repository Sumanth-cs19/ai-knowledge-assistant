namespace ai_knowledge_assistant.Application.Exceptions;

public sealed class ApplicationConfigurationException : ApplicationExceptionBase
{
    public ApplicationConfigurationException(string message)
        : base(message)
    {
    }
}
