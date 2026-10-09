namespace OrderProcessing.Application.Dtos;

public sealed record OrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record CreateOrderRequest(IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record UpdateStatusRequest(string Status);

public sealed record ListOrdersQuery(string? Status, int Page, int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
