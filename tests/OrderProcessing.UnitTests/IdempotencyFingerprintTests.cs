using FluentAssertions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Idempotency;

namespace OrderProcessing.UnitTests;

public class IdempotencyFingerprintTests
{
    private static CreateOrderRequest Request(params CreateOrderItemRequest[] items) => new(items);

    private static CreateOrderItemRequest Item(Guid productId, int quantity, decimal unitPrice) =>
        new(productId, quantity, unitPrice);

    [Fact]
    public void Trailing_zero_price_scales_normalize_the_same()
    {
        var product = Guid.NewGuid();

        var a = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 100m)));
        var b = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 100.00m)));
        var c = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 100.000m)));

        a.Should().Be(b);
        b.Should().Be(c);
    }

    [Fact]
    public void Extra_price_precision_is_rounded_before_hashing()
    {
        var product = Guid.NewGuid();

        var rounded = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 10.005m)));
        var literal = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 10.01m)));

        rounded.Should().Be(literal);
    }

    [Fact]
    public void Item_order_changes_the_fingerprint()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var a = IdempotencyRequestFingerprint.Compute(Request(Item(first, 1, 10m), Item(second, 1, 20m)));
        var b = IdempotencyRequestFingerprint.Compute(Request(Item(second, 1, 20m), Item(first, 1, 10m)));

        a.Should().NotBe(b);
    }

    [Fact]
    public void Duplicate_lines_are_preserved_and_change_the_fingerprint()
    {
        var product = Guid.NewGuid();

        var one = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 10m)));
        var two = IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 10m), Item(product, 1, 10m)));

        one.Should().NotBe(two);
    }

    [Fact]
    public void Quantity_changes_the_fingerprint()
    {
        var product = Guid.NewGuid();

        IdempotencyRequestFingerprint.Compute(Request(Item(product, 1, 10m)))
            .Should().NotBe(IdempotencyRequestFingerprint.Compute(Request(Item(product, 2, 10m))));
    }
}
