using FluentAssertions;
using OrderProcessing.Domain;

namespace OrderProcessing.UnitTests;

public class OrderTests
{
    private static OrderItem Item(int quantity = 1, decimal unitPrice = 10m) =>
        new(Guid.NewGuid(), quantity, unitPrice);

    [Fact]
    public void Create_with_one_item_starts_pending_and_computes_total()
    {
        var order = Order.Create([Item(quantity: 2, unitPrice: 100m)]);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(200m);
        order.Version.Should().Be(0);
    }

    [Fact]
    public void Create_with_multiple_items_sums_line_totals()
    {
        var order = Order.Create([Item(2, 100m), Item(1, 250m)]);

        order.TotalAmount.Should().Be(450m);
    }

    [Fact]
    public void Create_with_no_items_is_rejected()
    {
        var act = () => Order.Create([]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_EMPTY");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_quantity_is_rejected(int quantity)
    {
        var act = () => Order.Create([Item(quantity, 10m)]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ITEM_QUANTITY");
    }

    [Fact]
    public void Create_with_negative_price_is_rejected()
    {
        var act = () => Order.Create([Item(1, -0.01m)]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ITEM_PRICE");
    }

    [Fact]
    public void Line_total_rounds_half_up_at_two_decimals()
    {
        var item = new OrderItem(Guid.NewGuid(), 3, 1.005m);

        item.UnitPrice.Should().Be(1.01m);
        item.LineTotal.Should().Be(3.03m);
    }

    [Fact]
    public void Process_moves_pending_to_processing_and_bumps_version()
    {
        var order = Order.Create([Item()]);

        order.Process();

        order.Status.Should().Be(OrderStatus.Processing);
        order.UpdatedAt.Should().NotBeNull();
        order.Version.Should().Be(1);
    }

    [Fact]
    public void Process_from_non_pending_is_rejected()
    {
        var order = Order.Create([Item()]);
        order.Process();

        var act = () => order.Process();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Ship_then_deliver_follows_the_lifecycle()
    {
        var order = Order.Create([Item()]);

        order.Process();
        order.Ship();
        order.Deliver();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.Version.Should().Be(3);
    }

    [Fact]
    public void Ship_from_pending_is_rejected()
    {
        var order = Order.Create([Item()]);

        var act = () => order.Ship();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Deliver_from_processing_is_rejected()
    {
        var order = Order.Create([Item()]);
        order.Process();

        var act = () => order.Deliver();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Process_from_a_terminal_state_is_rejected()
    {
        var order = Order.Create([Item()]);
        order.Cancel();

        var act = () => order.Process();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Ship_from_shipped_is_rejected()
    {
        var order = Order.Create([Item()]);
        order.Process();
        order.Ship();

        var act = () => order.Ship();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Cancel_from_pending_moves_to_cancelled()
    {
        var order = Order.Create([Item()]);

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_from_non_pending_is_rejected(OrderStatus target)
    {
        var order = Order.Create([Item()]);
        MoveTo(order, target);

        var act = () => order.Cancel();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_NOT_CANCELLABLE");
    }

    private static void MoveTo(Order order, OrderStatus target)
    {
        switch (target)
        {
            case OrderStatus.Pending:
                return;
            case OrderStatus.Processing:
                order.Process();
                return;
            case OrderStatus.Shipped:
                order.Process();
                order.Ship();
                return;
            case OrderStatus.Delivered:
                order.Process();
                order.Ship();
                order.Deliver();
                return;
            case OrderStatus.Cancelled:
                order.Cancel();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
