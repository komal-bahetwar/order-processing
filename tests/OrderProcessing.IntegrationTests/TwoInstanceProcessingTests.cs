using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Services;

namespace OrderProcessing.IntegrationTests;

public class TwoInstanceProcessingTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public TwoInstanceProcessingTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Two_instances_move_a_pending_order_exactly_once()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest([new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        response.EnsureSuccessStatusCode();
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;

        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IOrderProcessingService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IOrderProcessingService>();

        var results = await Task.WhenAll(
            first.ProcessPendingOrdersAsync(),
            second.ProcessPendingOrdersAsync());

        results.Sum().Should().Be(1);

        var moved = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        moved!.Status.Should().Be("PROCESSING");
    }
}
