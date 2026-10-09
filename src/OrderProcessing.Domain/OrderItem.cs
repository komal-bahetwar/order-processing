namespace OrderProcessing.Domain;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("INVALID_ITEM_PRODUCT", "A product id is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("INVALID_ITEM_QUANTITY", "Item quantity must be greater than zero.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = Money.NormalizePrice(unitPrice);

        // Validate the line total at creation so an out-of-range product cannot persist.
        _ = Money.LineTotal(Quantity, UnitPrice);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Money.LineTotal(Quantity, UnitPrice);
}
