namespace ai_knowledge_assistant.Application.Exceptions;

public sealed class UnauthorizedRequestException : ApplicationExceptionBase
{
    public UnauthorizedRequestException(string message)
        : base(message)
    {
    }
}
