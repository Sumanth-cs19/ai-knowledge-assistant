namespace ai_knowledge_assistant.Application.Exceptions;

public sealed class ValidationException : ApplicationExceptionBase
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}
