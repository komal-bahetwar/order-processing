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

    public OrderProcessingService(
        IUnitOfWork unitOfWork,
        IOptions<OrderProcessingOptions> options,
        ILogger<OrderProcessingService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _batchSize = options.Value.BatchSize;
    }

    public async Task<int> ProcessPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var pendingIds = await _unitOfWork.Orders.GetPendingIdsAsync(_batchSize, cancellationToken);

        var processed = 0;
        foreach (var id in pendingIds)
        {
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
                _logger.LogInformation(
                    "Order {OrderId} transitioned {OldStatus} -> {NewStatus}",
                    order.Id,
                    OrderStatus.Pending,
                    order.Status);
                OrderMetrics.Transitions.Add(
                    1,
                    new KeyValuePair<string, object?>("from", "PENDING"),
                    new KeyValuePair<string, object?>("to", "PROCESSING"));
            }
            catch (ConcurrencyConflictException)
            {
                _unitOfWork.ClearChanges();
                _logger.LogInformation(
                    "Order {OrderId} was changed by another writer before the automatic move; skipping.",
                    id);
            }
            catch (DomainException exception)
            {
                _unitOfWork.ClearChanges();
                _logger.LogInformation(
                    "Order {OrderId} was not moved by the automatic run: {Code}.",
                    id,
                    exception.Code);
            }
        }

        return processed;
    }
}
