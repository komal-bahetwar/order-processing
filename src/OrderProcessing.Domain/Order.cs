using System.Collections.ObjectModel;

namespace OrderProcessing.Domain;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];
    private readonly ReadOnlyCollection<OrderItem> _readOnlyItems;

    private Order()
    {
        _readOnlyItems = _items.AsReadOnly();
    }

    private Order(Guid id, DateTimeOffset createdAt, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
        {
            throw new DomainException("ORDER_EMPTY", "An order must contain at least one item.");
        }

        Id = id;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
        Version = 0;
        _items.AddRange(items);
        _readOnlyItems = _items.AsReadOnly();
        TotalAmount = ComputeTotal(_items);
    }

    public Guid Id { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public int Version { get; private set; }

    public decimal TotalAmount { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _readOnlyItems;

    public static Order Create(IEnumerable<OrderItem>? items)
    {
        if (items is null)
        {
            throw new DomainException("ORDER_EMPTY", "An order must contain at least one item.");
        }

        var materialized = new List<OrderItem>();
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new DomainException("ORDER_ITEM_NULL", "An order item must not be null.");
            }

            materialized.Add(item);
        }

        return new Order(Guid.NewGuid(), DateTimeOffset.UtcNow, materialized);
    }

    public void Process() =>
        Transition(OrderStatus.Processing, "INVALID_ORDER_STATE", "Only pending orders can be processed.");

    public void Ship() =>
        Transition(OrderStatus.Shipped, "INVALID_ORDER_STATE", "Only processing orders can be shipped.");

    public void Deliver() =>
        Transition(OrderStatus.Delivered, "INVALID_ORDER_STATE", "Only shipped orders can be delivered.");

    public void Cancel() =>
        Transition(OrderStatus.Cancelled, "ORDER_NOT_CANCELLABLE", "Only pending orders can be cancelled.");

    public static bool IsLegal(OrderStatus from, OrderStatus to) => (from, to) switch
    {
        (OrderStatus.Pending, OrderStatus.Processing) => true,
        (OrderStatus.Pending, OrderStatus.Cancelled) => true,
        (OrderStatus.Processing, OrderStatus.Shipped) => true,
        (OrderStatus.Shipped, OrderStatus.Delivered) => true,
        _ => false
    };

    private static decimal ComputeTotal(IReadOnlyList<OrderItem> items)
    {
        var total = 0m;
        foreach (var item in items)
        {
            total = Money.AddChecked(total, item.LineTotal);
        }

        return total;
    }

    private void Transition(OrderStatus next, string code, string message)
    {
        if (!IsLegal(Status, next))
        {
            throw new DomainException(code, message);
        }

        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}
