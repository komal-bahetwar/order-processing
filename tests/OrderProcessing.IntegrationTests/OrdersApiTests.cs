using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Services;

namespace OrderProcessing.IntegrationTests;

public class OrdersApiTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public OrdersApiTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_returns_created_with_derived_total()
    {
        var response = await CreateOrderAsync(
            new CreateOrderItemRequest(Guid.NewGuid(), 2, 100m),
            new CreateOrderItemRequest(Guid.NewGuid(), 1, 250m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order!.Status.Should().Be("PENDING");
        order.TotalAmount.Should().Be(450m);
        order.Items.Should().HaveCount(2);
        order.Items.Should().OnlyContain(item => item.LineTotal > 0);
    }

    [Fact]
    public async Task Create_without_items_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest([]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_non_positive_quantity_returns_bad_request()
    {
        var response = await CreateOrderAsync(new CreateOrderItemRequest(Guid.NewGuid(), 0, 10m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_by_id_returns_the_order()
    {
        var created = await CreateAndReadOrderAsync();

        var fetched = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{created.Id}");

        fetched!.Id.Should().Be(created.Id);
        fetched.TotalAmount.Should().Be(created.TotalAmount);
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_not_found()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_by_malformed_id_returns_bad_request()
    {
        var response = await _client.GetAsync("/api/orders/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var order = await CreateAndReadOrderAsync();

        var pending = await _client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?status=PENDING");
        pending!.Items.Should().Contain(item => item.Id == order.Id);

        var shipped = await _client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?status=SHIPPED");
        shipped!.Items.Should().NotContain(item => item.Id == order.Id);
    }

    [Fact]
    public async Task List_clamps_a_request_over_the_maximum_bound()
    {
        await CreateAndReadOrderAsync();

        var result = await _client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?pageSize=500");

        result!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Advance_status_moves_processing_orders_to_shipped()
    {
        var order = await CreateAndReadOrderAsync();
        await ProcessPendingAsync(order.Id);

        var response = await _client.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/status", new UpdateStatusRequest("Shipped"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>();
        updated!.Status.Should().Be("SHIPPED");
    }

    [Fact]
    public async Task Advance_status_rejects_a_manual_pending_to_processing_move()
    {
        var order = await CreateAndReadOrderAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/status", new UpdateStatusRequest("Processing"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancel_a_pending_order_succeeds()
    {
        var order = await CreateAndReadOrderAsync();

        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<OrderDto>();
        cancelled!.Status.Should().Be("CANCELLED");
    }

    [Fact]
    public async Task Cancel_a_processing_order_is_rejected()
    {
        var order = await CreateAndReadOrderAsync();
        await ProcessPendingAsync(order.Id);

        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpResponseMessage> CreateOrderAsync(params CreateOrderItemRequest[] items) =>
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(items));

    private async Task<OrderDto> CreateAndReadOrderAsync()
    {
        var response = await CreateOrderAsync(new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private async Task ProcessPendingAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var processing = scope.ServiceProvider.GetRequiredService<IOrderProcessingService>();
        await processing.ProcessPendingOrdersAsync();

        var order = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{orderId}");
        order!.Status.Should().Be("PROCESSING");
    }
}
