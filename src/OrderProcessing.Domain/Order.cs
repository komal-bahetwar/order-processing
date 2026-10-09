namespace OrderProcessing.Domain;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    private Order(Guid id, DateTimeOffset createdAt, IEnumerable<OrderItem> items)
    {
        var materialized = items.ToList();
        if (materialized.Count == 0)
        {
            throw new DomainException("ORDER_EMPTY", "An order must contain at least one item.");
        }

        Id = id;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
        Version = 0;
        _items.AddRange(materialized);
        TotalAmount = _items.Sum(item => item.LineTotal);
    }

    public Guid Id { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public int Version { get; private set; }

    public decimal TotalAmount { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public static Order Create(IEnumerable<OrderItem> items) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, items);

    public void Process()
    {
        EnsureStatus(OrderStatus.Pending, "INVALID_ORDER_STATE", "Only pending orders can be processed.");
        TransitionTo(OrderStatus.Processing);
    }

    public void Ship()
    {
        EnsureStatus(OrderStatus.Processing, "INVALID_ORDER_STATE", "Only processing orders can be shipped.");
        TransitionTo(OrderStatus.Shipped);
    }

    public void Deliver()
    {
        EnsureStatus(OrderStatus.Shipped, "INVALID_ORDER_STATE", "Only shipped orders can be delivered.");
        TransitionTo(OrderStatus.Delivered);
    }

    public void Cancel()
    {
        EnsureStatus(OrderStatus.Pending, "ORDER_NOT_CANCELLABLE", "Only pending orders can be cancelled.");
        TransitionTo(OrderStatus.Cancelled);
    }

    private void EnsureStatus(OrderStatus expected, string code, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(code, message);
        }
    }

    private void TransitionTo(OrderStatus next)
    {
        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}
