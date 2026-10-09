using OrderProcessing.Domain;

namespace OrderProcessing.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetAsync(
        OrderStatus? status,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(int batchSize, CancellationToken cancellationToken = default);

    void Add(Order order);
}
