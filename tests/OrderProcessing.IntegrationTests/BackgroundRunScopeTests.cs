using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Services;
using OrderProcessing.Infrastructure.Jobs;

namespace OrderProcessing.IntegrationTests;

public class BackgroundRunScopeTests
{
    [Fact]
    public async Task The_job_pushes_a_background_run_id_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOrderProcessingService, StubProcessingService>();
        using var provider = services.BuildServiceProvider();

        var logger = new CapturingLogger<ProcessPendingOrdersJob>();
        var job = new ProcessPendingOrdersJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger);

        await job.RunAsync(CancellationToken.None);

        logger.ScopeKeys.Should().Contain("BackgroundRunId");
    }

    private sealed class StubProcessingService : IOrderProcessingService
    {
        public Task<int> ProcessPendingOrdersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> ScopeKeys { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object>> values)
            {
                ScopeKeys.AddRange(values.Select(pair => pair.Key));
            }

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
