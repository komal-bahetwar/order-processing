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
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest([new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        response.EnsureSuccessStatusCode();
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;

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
}
