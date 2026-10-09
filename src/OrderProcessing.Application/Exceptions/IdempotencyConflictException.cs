namespace OrderProcessing.Application.Exceptions;

/// <summary>
/// Raised when a concurrent request has already claimed the same idempotency key.
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("A concurrent request already claimed the idempotency key.")
    {
    }
}
