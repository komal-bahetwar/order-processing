using OrderProcessing.Application.Dtos;

namespace OrderProcessing.Application.Services;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> ListAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default);

    Task<OrderDto> UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);

    Task<OrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
