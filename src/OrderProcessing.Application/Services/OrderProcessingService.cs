using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Application.Observability;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Services;

public sealed class OrderProcessingService : IOrderProcessingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderProcessingService> _logger;
    private readonly int _batchSize;
    private readonly int _maxOrdersPerRun;

    public OrderProcessingService(
        IUnitOfWork unitOfWork,
        IOptions<OrderProcessingOptions> options,
        ILogger<OrderProcessingService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _batchSize = options.Value.BatchSize;
        _maxOrdersPerRun = options.Value.MaxOrdersPerRun;
    }

    public async Task<int> ProcessPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var backgroundRunId = Guid.NewGuid().ToString("N");
        var started = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "Automatic move run {BackgroundRunId} starting; batch {BatchSize}, order budget {Budget}.",
            backgroundRunId,
            _batchSize,
            _maxOrdersPerRun);

        var processed = 0;
        var selected = 0;
        var skipped = 0;
        var rounds = Math.Max(1, _maxOrdersPerRun / _batchSize) + 1;

        for (var round = 0; round < rounds && processed < _maxOrdersPerRun; round++)
        {
            var pendingIds = await _unitOfWork.Orders.GetPendingIdsAsync(_batchSize, cancellationToken);
            if (pendingIds.Count == 0)
            {
                break;
            }

            selected += pendingIds.Count;
            var progressed = false;
            foreach (var id in pendingIds)
            {
                if (processed >= _maxOrdersPerRun)
                {
                    break;
                }

                try
                {
                    var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken);
                    if (order is null || order.Status != OrderStatus.Pending)
                    {
                        continue;
                    }

                    order.Process();
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    processed++;
                    progressed = true;
                    _logger.LogInformation(
                        "Order {OrderId} transitioned {OldStatus} -> {NewStatus} in run {BackgroundRunId}.",
                        order.Id,
                        OrderStatus.Pending,
                        order.Status,
                        backgroundRunId);
                    OrderMetrics.Transitions.Add(
                        1,
                        new KeyValuePair<string, object?>("from", "PENDING"),
                        new KeyValuePair<string, object?>("to", "PROCESSING"));
                }
                catch (ConcurrencyConflictException)
                {
                    _unitOfWork.ClearChanges();
                    skipped++;
                    _logger.LogDebug("Order {OrderId} changed by another writer; skipping.", id);
                }
                catch (DomainException exception)
                {
                    _unitOfWork.ClearChanges();
                    skipped++;
                    _logger.LogDebug("Order {OrderId} was not moved by the automatic run: {Code}.", id, exception.Code);
                }
            }

            if (!progressed)
            {
                break;
            }
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        var budgetExhausted = processed >= _maxOrdersPerRun;
        _logger.LogInformation(
            "Automatic move run {BackgroundRunId} completed; processed {Processed}, skipped {Skipped}, selected {Selected}, budgetExhausted {BudgetExhausted}, in {ElapsedMs} ms.",
            backgroundRunId,
            processed,
            skipped,
            selected,
            budgetExhausted,
            elapsed.TotalMilliseconds);

        return processed;
    }
}
