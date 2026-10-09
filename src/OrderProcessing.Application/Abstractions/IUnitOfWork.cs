namespace OrderProcessing.Application.Abstractions;

public interface IUnitOfWork
{
    IOrderRepository Orders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void ClearChanges();
}
