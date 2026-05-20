using System.Net;
using OpsPilotAI.Common.Exceptions;

namespace OpsPilotAI.Infrastructure.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns a consistent JSON error envelope.
///
/// Key improvements over the original:
///   - Stack trace is ONLY included in Development (was leaked in all environments)
///   - PipelineException mapped to 422 Unprocessable Entity (it's a semantic failure, not 400/500)
///   - Uses IWebHostEnvironment to distinguish environments
///   - ErrorResponse is a record for immutability
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger,
    IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled exception — {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            PipelineException => (HttpStatusCode.UnprocessableEntity, exception.Message),
            ArgumentNullException or ArgumentException => (HttpStatusCode.BadRequest, "Invalid request parameters."),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            TimeoutException or TaskCanceledException => (HttpStatusCode.RequestTimeout, "The request timed out."),
            HttpRequestException => (HttpStatusCode.ServiceUnavailable, "A downstream service is unavailable."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse(
            StatusCode: (int)statusCode,
            Message: message,
            Detail: env.IsDevelopment() ? exception.Message : null,
            StackTrace: env.IsDevelopment() ? exception.StackTrace : null,
            Timestamp: DateTime.UtcNow
        );

        return context.Response.WriteAsJsonAsync(response);
    }
}

public sealed record ErrorResponse(
    int StatusCode,
    string Message,
    string? Detail,
    string? StackTrace,
    DateTime Timestamp);
