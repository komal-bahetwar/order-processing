using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Exceptions;

namespace OrderProcessing.IntegrationTests;

public class ConcurrencyTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public ConcurrencyTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_second_writer_with_a_stale_version_is_rejected()
    {
        var order = await CreateOrderAsync();

        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var firstReader = await first.Orders.GetByIdAsync(order.Id);
        var secondReader = await second.Orders.GetByIdAsync(order.Id);

        firstReader!.Cancel();
        await first.SaveChangesAsync();

        secondReader!.Process();
        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task A_batch_continues_after_one_order_conflicts()
    {
        var conflicting = await CreateOrderAsync();
        var healthy = await CreateOrderAsync();

        using var staleScope = _factory.Services.CreateScope();
        var stale = staleScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var conflictingReader = await stale.Orders.GetByIdAsync(conflicting.Id);
        await stale.Orders.GetByIdAsync(healthy.Id);

        using (var winnerScope = _factory.Services.CreateScope())
        {
            var winner = winnerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var winnerReader = await winner.Orders.GetByIdAsync(conflicting.Id);
            winnerReader!.Cancel();
            await winner.SaveChangesAsync();
        }

        conflictingReader!.Process();
        var act = async () => await stale.SaveChangesAsync();
        await act.Should().ThrowAsync<ConcurrencyConflictException>();

        stale.ClearChanges();

        var healthyReader = await stale.Orders.GetByIdAsync(healthy.Id);
        healthyReader!.Process();
        await stale.SaveChangesAsync();

        var checkedOrder = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{healthy.Id}");
        checkedOrder!.Status.Should().Be("PROCESSING");
    }

    private async Task<OrderDto> CreateOrderAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest([new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
