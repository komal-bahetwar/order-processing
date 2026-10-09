using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Services;

namespace OrderProcessing.IntegrationTests;

public class ProcessingServiceTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public ProcessingServiceTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task The_automatic_move_promotes_pending_orders_and_leaves_the_rest()
    {
        var pending = await CreateOrderAsync();
        var toCancel = await CreateOrderAsync();
        await _client.PostAsync($"/api/orders/{toCancel.Id}/cancel", null);

        await ProcessAsync();

        (await GetStatusAsync(pending.Id)).Should().Be("PROCESSING");
        (await GetStatusAsync(toCancel.Id)).Should().Be("CANCELLED");
    }

    [Fact]
    public async Task The_automatic_move_is_idempotent()
    {
        await CreateOrderAsync();

        var firstRun = await ProcessAsync();
        firstRun.Should().BeGreaterThanOrEqualTo(1);

        var secondRun = await ProcessAsync();
        secondRun.Should().Be(0);
    }

    private async Task<OrderDto> CreateOrderAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest([new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private async Task<int> ProcessAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var processing = scope.ServiceProvider.GetRequiredService<IOrderProcessingService>();
        return await processing.ProcessPendingOrdersAsync();
    }

    private async Task<string> GetStatusAsync(Guid id)
    {
        var order = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{id}");
        return order!.Status;
    }
}
