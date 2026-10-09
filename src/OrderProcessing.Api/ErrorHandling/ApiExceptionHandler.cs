using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Domain;

namespace OrderProcessing.Api.ErrorHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, title, detail) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while handling {Path}.", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                type = $"https://example.com/problems/{code.ToLowerInvariant().Replace('_', '-')}",
                title,
                status,
                code,
                detail,
                traceId = httpContext.TraceIdentifier
            },
            cancellationToken);

        return true;
    }

    private static (int Status, string Code, string Title, string Detail) Map(Exception exception) => exception switch
    {
        ValidationException validation => (
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            "Validation failed",
            string.Join("; ", validation.Errors.Select(failure => failure.ErrorMessage))),
        BadHttpRequestException badRequest => (
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            "Validation failed",
            badRequest.Message),
        ConcurrencyConflictException conflict => (
            StatusCodes.Status409Conflict,
            "CONCURRENCY_CONFLICT",
            "Concurrency conflict",
            conflict.Message),
        DomainException domain => MapDomain(domain),
        _ => (
            StatusCodes.Status500InternalServerError,
            "INTERNAL_ERROR",
            "Internal error",
            "The request could not be completed.")
    };

    private static (int Status, string Code, string Title, string Detail) MapDomain(DomainException domain) =>
        domain.Code switch
        {
            "ORDER_NOT_FOUND" => (
                StatusCodes.Status404NotFound, "ORDER_NOT_FOUND", "Order not found", domain.Message),
            "ORDER_NOT_CANCELLABLE" => (
                StatusCodes.Status409Conflict, "ORDER_NOT_CANCELLABLE", "Order not cancellable", domain.Message),
            "INVALID_ORDER_STATE" => (
                StatusCodes.Status409Conflict, "INVALID_ORDER_STATE", "Invalid order state", domain.Message),
            "INVALID_ID" => (
                StatusCodes.Status400BadRequest, "INVALID_ID", "Invalid id", domain.Message),
            _ => (
                StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Validation failed", domain.Message)
        };
}
