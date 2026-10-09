using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Services;
using OrderProcessing.Domain;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<CreateOrderRequest> _createValidator;
    private readonly IValidator<UpdateStatusRequest> _updateStatusValidator;
    private readonly IValidator<ListOrdersQuery> _listValidator;

    public OrdersController(
        IOrderService orderService,
        IValidator<CreateOrderRequest> createValidator,
        IValidator<UpdateStatusRequest> updateStatusValidator,
        IValidator<ListOrdersQuery> listValidator)
    {
        _orderService = orderService;
        _createValidator = createValidator;
        _updateStatusValidator = updateStatusValidator;
        _listValidator = listValidator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var order = await _orderService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(string id, CancellationToken cancellationToken) =>
        Ok(await _orderService.GetByIdAsync(ParseId(id), cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> List(
        [FromQuery] string? status,
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        await _listValidator.ValidateAndThrowAsync(
            new ListOrdersQuery(status, limit, cursor), cancellationToken);

        var page = await _orderService.ListAsync(status, limit, cursor, cancellationToken);

        if (page.NextCursor is not null)
        {
            Response.Headers["X-Next-Cursor"] = page.NextCursor;
        }

        return Ok(page.Items);
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        string id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _updateStatusValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _orderService.UpdateStatusAsync(ParseId(id), request.Status, cancellationToken));
    }

    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Cancel(string id, CancellationToken cancellationToken) =>
        Ok(await _orderService.CancelAsync(ParseId(id), cancellationToken));

    private static Guid ParseId(string id)
    {
        if (!Guid.TryParse(id, out var orderId))
        {
            throw new DomainException("INVALID_ID", $"'{id}' is not a valid order id.");
        }

        return orderId;
    }
}
