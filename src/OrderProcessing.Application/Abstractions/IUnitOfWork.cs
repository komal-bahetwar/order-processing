namespace OrderProcessing.Application.Abstractions;

public interface IUnitOfWork
{
    IOrderRepository Orders { get; }

    IIdempotencyStore Idempotency { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void ClearChanges();
}
