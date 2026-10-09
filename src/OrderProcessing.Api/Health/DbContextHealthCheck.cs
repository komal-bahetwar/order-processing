using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Api.Health;

public sealed class DbContextHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DbContextHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("The order data store is reachable.")
            : HealthCheckResult.Unhealthy("The order data store is not reachable.");
    }
}
