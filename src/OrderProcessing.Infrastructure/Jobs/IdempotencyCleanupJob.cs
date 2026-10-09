using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Infrastructure.Jobs;

public sealed class IdempotencyCleanupJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyCleanupJob> _logger;

    public IdempotencyCleanupJob(
        IServiceScopeFactory scopeFactory,
        ILogger<IdempotencyCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        // Safe across workers: a single delete of records past their expiry. Live
        // claims have a future expiry and are never touched.
        var removed = await store.DeleteExpiredAsync(timeProvider.GetUtcNow(), cancellationToken);

        _logger.LogInformation("Idempotency cleanup removed {Removed} expired records.", removed);
    }
}
