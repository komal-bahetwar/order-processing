using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Exceptions;
using OrderProcessing.Application.Idempotency;
using OrderProcessing.Application.Mapping;
using OrderProcessing.Application.Observability;
using OrderProcessing.Application.Pagination;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Services;

public sealed class OrderService : IOrderService
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly int _idempotencyRetentionHours;

    public OrderService(
        IUnitOfWork unitOfWork,
        ILogger<OrderService> logger,
        TimeProvider timeProvider,
        IOptions<OrderProcessingOptions> options)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
        _idempotencyRetentionHours = options.Value.IdempotencyRetentionHours;
    }

    public async Task<OrderDto> CreateAsync(
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            var plain = BuildOrder(request, _timeProvider.GetUtcNow());
            _unitOfWork.Orders.Add(plain);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return plain.ToDto();
        }

        const string scope = IdempotencyRecord.CreateOrderScope;
        var fingerprint = IdempotencyRequestFingerprint.Compute(request);
        var now = _timeProvider.GetUtcNow();

        var existing = await _unitOfWork.Idempotency.FindAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpiresAt > now)
            {
                return Replay(existing, fingerprint);
            }

            // Expired: reuse of the key is allowed again.
            _unitOfWork.Idempotency.Remove(existing);
        }

        var order = BuildOrder(request, now);
        _unitOfWork.Orders.Add(order);

        var dto = order.ToDto();
        _unitOfWork.Idempotency.Add(new IdempotencyRecord(
            scope,
            idempotencyKey,
            fingerprint,
            order.Id,
            JsonSerializer.Serialize(dto),
            "application/json",
            statusCode: 201,
            location: $"/api/orders/{order.Id}",
            createdAt: now,
            expiresAt: now.AddHours(_idempotencyRetentionHours)));

        try
        {
            // The order and its idempotency record commit in one transaction.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            // A concurrent request claimed the key first. Recover, then read the winner.
            _unitOfWork.ClearChanges();

            var winner = await _unitOfWork.Idempotency.FindAsync(scope, idempotencyKey, cancellationToken);
            if (winner is null)
            {
                throw new DomainException(
                    "IDEMPOTENCY_REQUEST_IN_PROGRESS",
                    "A request with this idempotency key is still being processed. Retry shortly.");
            }

            return Replay(winner, fingerprint);
        }

        return dto;
    }

    private static Order BuildOrder(CreateOrderRequest request, DateTimeOffset now)
    {
        var items = request.Items.Select(item => new OrderItem(item.ProductId, item.Quantity, item.UnitPrice));
        return Order.Create(items, now);
    }

    private static OrderDto Replay(IdempotencyRecord record, string fingerprint)
    {
        if (!string.Equals(record.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new DomainException(
                "IDEMPOTENCY_KEY_REUSED",
                "The idempotency key was already used with a different request.");
        }

        return JsonSerializer.Deserialize<OrderDto>(record.ResponseBody)
            ?? throw new DomainException("INTERNAL_ERROR", "The stored idempotent response could not be read.");
    }

    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        return order.ToDto();
    }

    public async Task<OrderPage> ListAsync(
        string? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        OrderStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!OrderStatusNames.TryParse(status, out var parsed))
            {
                throw new DomainException("VALIDATION_ERROR", $"'{status}' is not a known order status.");
            }

            filter = parsed;
        }

        DateTimeOffset? afterCreatedAt = null;
        Guid? afterId = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!OrderCursor.TryDecode(cursor, out var cursorCreatedAt, out var cursorId))
            {
                throw new DomainException("VALIDATION_ERROR", "The cursor is not valid.");
            }

            afterCreatedAt = cursorCreatedAt;
            afterId = cursorId;
        }

        var take = limit < 1 ? DefaultLimit : Math.Min(limit, MaxLimit);

        // Read one extra row to detect whether a further page exists.
        var orders = await _unitOfWork.Orders.GetAsync(
            filter, afterCreatedAt, afterId, take + 1, cancellationToken);

        string? nextCursor = null;
        if (orders.Count > take)
        {
            orders = orders.Take(take).ToList();
            var last = orders[^1];
            nextCursor = OrderCursor.Encode(last.CreatedAt, last.Id);
        }

        return new OrderPage(orders.Select(order => order.ToDto()).ToList(), nextCursor);
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        if (!OrderStatusNames.TryParse(status, out var target))
        {
            throw new DomainException("VALIDATION_ERROR", $"'{status}' is not a known order status.");
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        if (order.Status == target)
        {
            return order.ToDto();
        }

        var from = order.Status;
        switch (target)
        {
            case OrderStatus.Shipped:
                order.Ship(_timeProvider.GetUtcNow());
                break;
            case OrderStatus.Delivered:
                order.Deliver(_timeProvider.GetUtcNow());
                break;
            default:
                throw new DomainException(
                    "INVALID_ORDER_STATE",
                    $"A change to {target} is not an accepted manual transition.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        RecordTransition(order, from);

        return order.ToDto();
    }

    public async Task<OrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw NotFound(id);

        var from = order.Status;
        order.Cancel(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        RecordTransition(order, from);

        return order.ToDto();
    }

    private void RecordTransition(Order order, OrderStatus from)
    {
        _logger.LogInformation(
            "Order {OrderId} transitioned {OldStatus} -> {NewStatus}",
            order.Id,
            from,
            order.Status);

        OrderMetrics.Transitions.Add(
            1,
            new KeyValuePair<string, object?>("from", from.ToString().ToUpperInvariant()),
            new KeyValuePair<string, object?>("to", order.Status.ToString().ToUpperInvariant()));
    }

    private static DomainException NotFound(Guid id) =>
        new("ORDER_NOT_FOUND", $"Order '{id}' was not found.");
}
