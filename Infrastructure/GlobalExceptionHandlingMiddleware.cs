using System.Net;
using System.Text.Json;

namespace OpsPilotAI.Infrastructure
{
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

        public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred. Path: {Path}, Method: {Method}", context.Request.Path, context.Request.Method);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse();

            switch (exception)
            {
                case ArgumentNullException argNullEx:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = "Argument validation failed",
                        Details = argNullEx.Message,
                        Timestamp = DateTime.UtcNow
                    };
                    break;

                case ArgumentException argEx:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = "Invalid argument provided",
                        Details = argEx.Message,
                        Timestamp = DateTime.UtcNow
                    };
                    break;

                case InvalidOperationException invOpEx:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = "Invalid operation",
                        Details = invOpEx.Message,
                        Timestamp = DateTime.UtcNow
                    };
                    break;

                case TimeoutException timeoutEx:
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.RequestTimeout,
                        Message = "Request timeout",
                        Details = timeoutEx.Message,
                        Timestamp = DateTime.UtcNow
                    };
                    break;

                case HttpRequestException httpReqEx:
                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                        Message = "External service unavailable",
                        Details = httpReqEx.Message,
                        Timestamp = DateTime.UtcNow
                    };
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError,
                        Message = "An unexpected error occurred",
                        Details = exception.Message,
                        ExceptionType = exception.GetType().Name,
                        StackTrace = exception.StackTrace,
                        Timestamp = DateTime.UtcNow
                    };
                    break;
            }

            return context.Response.WriteAsJsonAsync(response);
        }
    }

    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
