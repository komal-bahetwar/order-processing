namespace OrderProcessing.Domain;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new DomainException("INVALID_ITEM_QUANTITY", "Item quantity must be greater than zero.");
        }

        if (unitPrice < 0m)
        {
            throw new DomainException("INVALID_ITEM_PRICE", "Item unit price must not be negative.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
