using OrderProcessing.Application.Dtos;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Mapping;

public static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.Status.ToString().ToUpperInvariant(),
        order.TotalAmount,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items
            .Select(item => new OrderItemDto(item.ProductId, item.Quantity, item.UnitPrice, item.LineTotal))
            .ToList());
}
