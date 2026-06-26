namespace ai_knowledge_assistant.Application.Exceptions;

public sealed class NotFoundException : ApplicationExceptionBase
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
