using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Services;

public sealed class OrderProcessingService : IOrderProcessingService
{
    private const int DefaultBatchSize = 200;

    private readonly IUnitOfWork _unitOfWork;

    public OrderProcessingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> ProcessPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _unitOfWork.Orders.GetPendingAsync(DefaultBatchSize, cancellationToken);

        var processed = 0;
        foreach (var order in pending)
        {
            try
            {
                order.Process();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (ConcurrencyConflictException)
            {
                // Another actor changed the order first; leave it for the winner.
            }
            catch (DomainException)
            {
                // The order already left PENDING; the move is idempotent.
            }
        }

        return processed;
    }
}
