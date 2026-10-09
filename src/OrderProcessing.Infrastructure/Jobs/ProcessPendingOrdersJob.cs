using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Services;

namespace OrderProcessing.Infrastructure.Jobs;

public sealed class ProcessPendingOrdersJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessPendingOrdersJob> _logger;

    public ProcessPendingOrdersJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ProcessPendingOrdersJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IOrderProcessingService>();

        var processed = await processingService.ProcessPendingOrdersAsync(cancellationToken);

        _logger.LogInformation("Automatic move processed {ProcessedCount} pending orders.", processed);
    }
}
