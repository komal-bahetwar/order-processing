using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Domain;

namespace OrderProcessing.Api.ErrorHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, detail) = Map(exception);

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = code,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static (int Status, string Code, string Detail) Map(Exception exception) => exception switch
    {
        ValidationException validation => (
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            string.Join("; ", validation.Errors.Select(failure => failure.ErrorMessage))),
        ConcurrencyConflictException conflict => (
            StatusCodes.Status409Conflict,
            "CONCURRENCY_CONFLICT",
            conflict.Message),
        DomainException domain => (MapDomainStatus(domain.Code), domain.Code, domain.Message),
        _ => (0, string.Empty, string.Empty)
    };

    private static int MapDomainStatus(string code) => code switch
    {
        "ORDER_NOT_FOUND" => StatusCodes.Status404NotFound,
        "ORDER_NOT_CANCELLABLE" or "ORDER_INVALID_STATE" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
