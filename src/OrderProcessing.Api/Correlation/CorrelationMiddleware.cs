using Serilog.Context;

namespace OrderProcessing.Api.Correlation;

public static class CorrelationHeaders
{
    public const string HeaderName = "X-Correlation-ID";

    private const int MaxLength = 64;

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxLength &&
        value.All(IsAllowed);

    public static string Generate() => Guid.NewGuid().ToString("N");

    private static bool IsAllowed(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-';
}

public sealed class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers[CorrelationHeaders.HeaderName];
        var invalid = values.Count > 1 || (values.Count == 1 && !CorrelationHeaders.IsValid(values[0]));
        var correlationId = invalid || values.Count == 0 ? CorrelationHeaders.Generate() : values[0]!;

        // Set the header as the response starts, so it survives the exception handler,
        // which clears response headers before it writes an error body.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeaders.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        if (invalid)
        {
            _logger.LogWarning(
                "Rejected an invalid {Header} header ({Count} value(s)).",
                CorrelationHeaders.HeaderName,
                values.Count);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://example.com/problems/validation-error",
                title = "Validation failed",
                status = StatusCodes.Status400BadRequest,
                code = "VALIDATION_ERROR",
                detail =
                    $"The {CorrelationHeaders.HeaderName} header must be a single value of at most 64 characters from letters, digits, '.', '_', and '-'.",
                traceId = context.TraceIdentifier
            });

            return;
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            await _next(context);
        }
    }
}
