using System.Reflection;
using FluentAssertions;
using Hangfire;
using OrderProcessing.Application;
using OrderProcessing.Infrastructure;
using OrderProcessing.Infrastructure.Jobs;

namespace OrderProcessing.IntegrationTests;

public class OptionsValidationTests
{
    private readonly OrderProcessingOptionsValidator _validator = new();

    [Fact]
    public void The_default_options_are_valid()
    {
        _validator.Validate(null, new OrderProcessingOptions()).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 5000, "*/5 * * * *")]
    [InlineData(200, 100, "*/5 * * * *")]
    [InlineData(200, 5000, "not a cron")]
    public void Invalid_configuration_fails_validation(int batchSize, int maxOrdersPerRun, string cron)
    {
        var options = new OrderProcessingOptions
        {
            BatchSize = batchSize,
            MaxOrdersPerRun = maxOrdersPerRun,
            CronExpression = cron
        };

        _validator.Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void The_job_declares_a_finite_retry_policy()
    {
        var method = typeof(ProcessPendingOrdersJob).GetMethod(nameof(ProcessPendingOrdersJob.RunAsync))!;
        var attribute = method.GetCustomAttribute<AutomaticRetryAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Attempts.Should().Be(3);
    }
}
