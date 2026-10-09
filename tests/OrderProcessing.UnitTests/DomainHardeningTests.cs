using System.Collections.ObjectModel;
using FluentAssertions;
using OrderProcessing.Domain;

namespace OrderProcessing.UnitTests;

public class DomainHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static OrderItem Item(int quantity = 1, decimal unitPrice = 10m) =>
        new(Guid.NewGuid(), quantity, unitPrice);

    private static Order NewOrder(params OrderItem[] items) => Order.Create(items, Now);

    [Fact]
    public void Items_is_a_read_only_collection_and_cannot_be_cast_to_a_list()
    {
        var order = NewOrder(Item());

        order.Items.Should().BeAssignableTo<ReadOnlyCollection<OrderItem>>();
        (order.Items as List<OrderItem>).Should().BeNull();
    }

    [Fact]
    public void Items_rejects_add_remove_and_clear()
    {
        var order = NewOrder(Item());
        var collection = (ICollection<OrderItem>)order.Items;

        ((Action)(() => collection.Add(Item()))).Should().Throw<NotSupportedException>();
        ((Action)(() => collection.Remove(Item()))).Should().Throw<NotSupportedException>();
        ((Action)(() => collection.Clear())).Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Mutating_the_caller_list_after_creation_does_not_change_the_order()
    {
        var input = new List<OrderItem> { Item(1, 10m) };
        var order = Order.Create(input, Now);

        input.Add(Item(5, 100m));

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(10m);
    }

    [Fact]
    public void Null_sequence_is_rejected()
    {
        Action act = () => Order.Create(null!, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_EMPTY");
    }

    [Fact]
    public void Null_element_is_rejected()
    {
        Action act = () => Order.Create([Item(), null!], Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ORDER_ITEM_NULL");
    }

    [Fact]
    public void Empty_product_id_is_rejected()
    {
        Action act = () => new OrderItem(Guid.Empty, 1, 10m);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_ITEM_PRODUCT");
    }

    [Fact]
    public void The_maximum_stored_amount_is_accepted()
    {
        var item = new OrderItem(Guid.NewGuid(), 1, Money.MaxStoredAmount);

        item.UnitPrice.Should().Be(Money.MaxStoredAmount);
    }

    [Fact]
    public void A_price_above_the_ceiling_is_rejected()
    {
        Action act = () => new OrderItem(Guid.NewGuid(), 1, Money.MaxStoredAmount + 0.01m);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("AMOUNT_OUT_OF_RANGE");
    }

    [Fact]
    public void A_line_total_above_the_ceiling_is_rejected()
    {
        Action act = () => new OrderItem(Guid.NewGuid(), 2, Money.MaxStoredAmount);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("AMOUNT_OUT_OF_RANGE");
    }

    [Fact]
    public void A_total_above_the_ceiling_is_rejected()
    {
        var half = 5_000_000_000_000_000.00m;
        var first = new OrderItem(Guid.NewGuid(), 1, half);
        var second = new OrderItem(Guid.NewGuid(), 1, half);

        Action act = () => Order.Create([first, second], Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("AMOUNT_OUT_OF_RANGE");
    }

    [Fact]
    public void A_zero_priced_item_is_accepted()
    {
        var order = NewOrder(new OrderItem(Guid.NewGuid(), 3, 0m));

        order.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public void Extreme_amounts_are_rejected_without_an_overflow()
    {
        Action act = () => new OrderItem(Guid.NewGuid(), int.MaxValue, decimal.MaxValue);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Exactly_four_of_the_twenty_five_state_pairs_are_legal()
    {
        var legal = 0;
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                if (Order.IsLegal(from, to))
                {
                    legal++;
                }
            }
        }

        legal.Should().Be(4);
    }

    public static IEnumerable<object[]> StateMethodCases()
    {
        string[] methods = ["Process", "Ship", "Deliver", "Cancel"];
        foreach (var state in Enum.GetValues<OrderStatus>())
        {
            foreach (var method in methods)
            {
                yield return [state, method];
            }
        }
    }

    [Theory]
    [MemberData(nameof(StateMethodCases))]
    public void Transition_matrix_holds(OrderStatus start, string method)
    {
        var order = NewOrder(Item());
        MoveTo(order, start);
        var versionBefore = order.Version;

        var target = method switch
        {
            "Process" => OrderStatus.Processing,
            "Ship" => OrderStatus.Shipped,
            "Deliver" => OrderStatus.Delivered,
            _ => OrderStatus.Cancelled
        };
        var legal = Order.IsLegal(start, target);

        Action act = () => Invoke(order, method);

        if (legal)
        {
            act.Should().NotThrow();
            order.Status.Should().Be(target);
            order.Version.Should().Be(versionBefore + 1);
        }
        else
        {
            act.Should().Throw<DomainException>();
            order.Status.Should().Be(start);
            order.Version.Should().Be(versionBefore);
        }
    }

    [Theory]
    [InlineData("SHIPPED", true)]
    [InlineData("shipped", true)]
    [InlineData("Processing", true)]
    [InlineData("  Delivered ", true)]
    [InlineData("999", false)]
    [InlineData("0", false)]
    [InlineData("SHIP", false)]
    [InlineData("", false)]
    public void Status_names_parse_by_name_only(string input, bool expected)
    {
        OrderStatusNames.TryParse(input, out _).Should().Be(expected);
    }

    private static void Invoke(Order order, string method)
    {
        switch (method)
        {
            case "Process": order.Process(Now); break;
            case "Ship": order.Ship(Now); break;
            case "Deliver": order.Deliver(Now); break;
            case "Cancel": order.Cancel(Now); break;
            default: throw new ArgumentOutOfRangeException(nameof(method));
        }
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
