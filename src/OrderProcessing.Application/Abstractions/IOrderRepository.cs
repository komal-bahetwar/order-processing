using OrderProcessing.Domain;

namespace OrderProcessing.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedAsync(
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    void Add(Order order);
}
