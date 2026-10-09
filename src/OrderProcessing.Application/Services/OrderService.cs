using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Mapping;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Services;

public sealed class OrderService : IOrderService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IUnitOfWork _unitOfWork;

    public OrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var items = request.Items.Select(item => new OrderItem(item.ProductId, item.Quantity, item.UnitPrice));
        var order = Order.Create(items);

        _unitOfWork.Orders.Add(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }

    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        return order.ToDto();
    }

    public async Task<PagedResult<OrderDto>> ListAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        OrderStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            {
                throw new DomainException("VALIDATION_ERROR", $"'{status}' is not a known order status.");
            }

            filter = parsed;
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var (items, total) = await _unitOfWork.Orders.GetPagedAsync(
            filter, (page - 1) * pageSize, pageSize, cancellationToken);

        return new PagedResult<OrderDto>(
            items.Select(order => order.ToDto()).ToList(),
            page,
            pageSize,
            total);
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var target))
        {
            throw new DomainException("VALIDATION_ERROR", $"'{status}' is not a known order status.");
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        if (order.Status == target)
        {
            return order.ToDto();
        }

        switch (target)
        {
            case OrderStatus.Shipped:
                order.Ship();
                break;
            case OrderStatus.Delivered:
                order.Deliver();
                break;
            default:
                throw new DomainException(
                    "ORDER_INVALID_STATE",
                    $"A change to {target} is not an accepted manual transition.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }

    public async Task<OrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        order.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }

    private static DomainException NotFound(Guid id) =>
        new("ORDER_NOT_FOUND", $"Order '{id}' was not found.");
}
