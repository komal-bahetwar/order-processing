namespace OrderProcessing.Application.Exceptions;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Guid orderId)
        : base($"The order '{orderId}' was changed by another request.")
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; }
}
