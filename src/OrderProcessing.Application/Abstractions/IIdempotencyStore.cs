using OrderProcessing.Application.Idempotency;

namespace OrderProcessing.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string scope, string key, CancellationToken cancellationToken = default);

    void Add(IdempotencyRecord record);

    void Remove(IdempotencyRecord record);

    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
