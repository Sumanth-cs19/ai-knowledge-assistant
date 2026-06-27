using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Common;

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
            await Results.ValidationProblem(
                    exception.Errors,
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
        catch (ConflictException exception)
        {
            await Results.Problem(
                    title: "Conflict",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
        catch (UnauthorizedRequestException exception)
        {
            await Results.Problem(
                    title: "Unauthorized",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status401Unauthorized,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
        catch (NotFoundException exception)
        {
            await Results.Problem(
                    title: "Not Found",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
        catch (ApplicationConfigurationException exception)
        {
            _logger.LogError(exception, "Application configuration or reference data validation failed.");

            await Results.Problem(
                    title: "Application configuration error",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
        catch (AIProviderException exception)
        {
            _logger.LogError(exception, "AI provider request failed.");

            await Results.Problem(
                    title: "AI provider error",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred while processing the request.");

            await Results.Problem(
                    title: "Unexpected error",
                    detail: "An unexpected error occurred while processing the request.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    extensions: GetProblemExtensions(context))
                .ExecuteAsync(context);
        }
    }

    private static Dictionary<string, object?> GetProblemExtensions(HttpContext context)
    {
        var correlationId = context.Items.TryGetValue(Observability.CorrelationIdHeader, out var value)
            ? value?.ToString()
            : null;

        return string.IsNullOrWhiteSpace(correlationId)
            ? []
            : new Dictionary<string, object?> { ["correlationId"] = correlationId };
    }
}
