using ai_knowledge_assistant.Application.Exceptions;

namespace ai_knowledge_assistant.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException exception)
        {
            await Results.ValidationProblem(exception.Errors, statusCode: StatusCodes.Status400BadRequest)
                .ExecuteAsync(context);
        }
        catch (ConflictException exception)
        {
            await Results.Problem(
                    title: "Conflict",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict)
                .ExecuteAsync(context);
        }
        catch (UnauthorizedRequestException exception)
        {
            await Results.Problem(
                    title: "Unauthorized",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred while processing the request.");

            await Results.Problem(
                    title: "Unexpected error",
                    detail: "An unexpected error occurred while processing the request.",
                    statusCode: StatusCodes.Status500InternalServerError)
                .ExecuteAsync(context);
        }
    }
}
