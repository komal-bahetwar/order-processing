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
    public async Task Two_instances_move_a_backlog_exactly_once()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var created = await _client.PostAsJsonAsync(
                "/api/orders",
                new CreateOrderRequest([new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
            created.EnsureSuccessStatusCode();
            ids.Add((await created.Content.ReadFromJsonAsync<OrderDto>())!.Id);
        }

        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IOrderProcessingService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IOrderProcessingService>();

        var results = await Task.WhenAll(
            first.ProcessPendingOrdersAsync(),
            second.ProcessPendingOrdersAsync());

        // Races are resolved by the version token, so each order moves exactly once.
        results.Sum().Should().Be(3);

        foreach (var id in ids)
        {
            var order = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{id}");
            order!.Status.Should().Be("PROCESSING");
        }
    }
}
