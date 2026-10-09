using FluentAssertions;
using OrderProcessing.Domain;

namespace OrderProcessing.UnitTests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static OrderItem Item(int quantity = 1, decimal unitPrice = 10m) =>
        new(Guid.NewGuid(), quantity, unitPrice);

    private static Order NewOrder(params OrderItem[] items) => Order.Create(items, Now);

    [Fact]
    public void Create_with_one_item_starts_pending_and_computes_total()
    {
        var order = NewOrder(Item(quantity: 2, unitPrice: 100m));

        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(200m);
        order.Version.Should().Be(0);
    }

    [Fact]
    public void Create_uses_the_supplied_timestamp_normalized_to_utc()
    {
        var offset = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.FromHours(5));
        var order = Order.Create([Item()], offset);

        order.CreatedAt.Should().Be(offset.ToUniversalTime());
        order.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
        order.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_with_multiple_items_sums_line_totals()
    {
        var order = NewOrder(Item(2, 100m), Item(1, 250m));

        order.TotalAmount.Should().Be(450m);
    }

    [Fact]
    public void Create_with_no_items_is_rejected()
    {
        var act = () => Order.Create([], Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_EMPTY");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_quantity_is_rejected(int quantity)
    {
        var act = () => NewOrder(Item(quantity, 10m));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ITEM_QUANTITY");
    }

    [Fact]
    public void Create_with_negative_price_is_rejected()
    {
        var act = () => NewOrder(Item(1, -0.01m));

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
        var order = NewOrder(Item());

        order.Process(Now);

        order.Status.Should().Be(OrderStatus.Processing);
        order.UpdatedAt.Should().Be(Now);
        order.Version.Should().Be(1);
        order.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Process_from_non_pending_is_rejected()
    {
        var order = NewOrder(Item());
        order.Process(Now);

        var act = () => order.Process(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void A_failed_transition_leaves_the_timestamps_unchanged()
    {
        var order = NewOrder(Item());
        order.Process(Now);
        var updatedAt = order.UpdatedAt;

        var act = () => order.Process(Now.AddHours(1));

        act.Should().Throw<DomainException>();
        order.UpdatedAt.Should().Be(updatedAt);
        order.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Ship_then_deliver_follows_the_lifecycle()
    {
        var order = NewOrder(Item());

        order.Process(Now);
        order.Ship(Now);
        order.Deliver(Now);

        order.Status.Should().Be(OrderStatus.Delivered);
        order.Version.Should().Be(3);
    }

    [Fact]
    public void Ship_from_pending_is_rejected()
    {
        var order = NewOrder(Item());

        var act = () => order.Ship(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Deliver_from_processing_is_rejected()
    {
        var order = NewOrder(Item());
        order.Process(Now);

        var act = () => order.Deliver(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Process_from_a_terminal_state_is_rejected()
    {
        var order = NewOrder(Item());
        order.Cancel(Now);

        var act = () => order.Process(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Ship_from_shipped_is_rejected()
    {
        var order = NewOrder(Item());
        order.Process(Now);
        order.Ship(Now);

        var act = () => order.Ship(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ORDER_STATE");
    }

    [Fact]
    public void Cancel_from_pending_moves_to_cancelled()
    {
        var order = NewOrder(Item());

        order.Cancel(Now);

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_from_non_pending_is_rejected(OrderStatus target)
    {
        var order = NewOrder(Item());
        MoveTo(order, target);

        var act = () => order.Cancel(Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_NOT_CANCELLABLE");
    }

    private static void MoveTo(Order order, OrderStatus target)
    {
        switch (target)
        {
            case OrderStatus.Pending:
                return;
            case OrderStatus.Processing:
                order.Process(Now);
                return;
            case OrderStatus.Shipped:
                order.Process(Now);
                order.Ship(Now);
                return;
            case OrderStatus.Delivered:
                order.Process(Now);
                order.Ship(Now);
                order.Deliver(Now);
                return;
            case OrderStatus.Cancelled:
                order.Cancel(Now);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
