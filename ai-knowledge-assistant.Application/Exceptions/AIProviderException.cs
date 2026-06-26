namespace ai_knowledge_assistant.Application.Exceptions;

public sealed class AIProviderException : ApplicationExceptionBase
{
    public AIProviderException(string message)
        : base(message)
    {
    }

    public AIProviderException(string message, Exception innerException)
        : base(message)
    {
        InnerExceptionDetail = innerException;
    }

    public Exception? InnerExceptionDetail { get; }
}
