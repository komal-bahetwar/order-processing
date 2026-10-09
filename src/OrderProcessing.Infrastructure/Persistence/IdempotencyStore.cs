using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Idempotency;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly AppDbContext _dbContext;

    public IdempotencyStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IdempotencyRecord?> FindAsync(
        string scope,
        string key,
        CancellationToken cancellationToken = default) =>
        _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(record => record.Scope == scope && record.Key == key, cancellationToken);

    public void Add(IdempotencyRecord record) => _dbContext.IdempotencyRecords.Add(record);

    public void Remove(IdempotencyRecord record) => _dbContext.IdempotencyRecords.Remove(record);

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        _dbContext.IdempotencyRecords
            .Where(record => record.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
}
