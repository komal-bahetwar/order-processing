namespace OrderProcessing.Application.Services;

public interface IOrderProcessingService
{
    Task<int> ProcessPendingOrdersAsync(CancellationToken cancellationToken = default);
}
