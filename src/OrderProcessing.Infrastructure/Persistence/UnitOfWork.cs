using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Domain;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext, IOrderRepository orders)
    {
        _dbContext = dbContext;
        Orders = orders;
    }

    public IOrderRepository Orders { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var order = exception.Entries
                .Select(entry => entry.Entity)
                .OfType<Order>()
                .FirstOrDefault();

            throw new ConcurrencyConflictException(order?.Id ?? Guid.Empty);
        }
    }
}
