using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Domain;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext, IOrderRepository orders, IIdempotencyStore idempotency)
    {
        _dbContext = dbContext;
        Orders = orders;
        Idempotency = idempotency;
    }

    public IOrderRepository Orders { get; }

    public IIdempotencyStore Idempotency { get; }

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
        catch (DbUpdateException exception) when (IsIdempotencyUniqueViolation(exception))
        {
            throw new IdempotencyConflictException();
        }
    }

    public void ClearChanges() => _dbContext.ChangeTracker.Clear();

    private static bool IsIdempotencyUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: "23505" } postgres &&
        postgres.ConstraintName == "ux_idempotency_records_scope_key";
}
