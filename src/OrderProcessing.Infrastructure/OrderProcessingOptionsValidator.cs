using Cronos;
using Microsoft.Extensions.Options;
using OrderProcessing.Application;

namespace OrderProcessing.Infrastructure;

public sealed class OrderProcessingOptionsValidator : IValidateOptions<OrderProcessingOptions>
{
    public const int MaxBatchSize = 10_000;

    public ValidateOptionsResult Validate(string? name, OrderProcessingOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize < 1 || options.BatchSize > MaxBatchSize)
        {
            failures.Add($"OrderProcessing:BatchSize must be between 1 and {MaxBatchSize}.");
        }

        if (options.MaxOrdersPerRun < options.BatchSize)
        {
            failures.Add("OrderProcessing:MaxOrdersPerRun must be at least OrderProcessing:BatchSize.");
        }

        try
        {
            CronExpression.Parse(options.CronExpression);
        }
        catch (Exception exception)
        {
            failures.Add($"OrderProcessing:CronExpression '{options.CronExpression}' is not valid: {exception.Message}");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
