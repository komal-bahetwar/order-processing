using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Services;

namespace OrderProcessing.IntegrationTests;

public sealed class SmallBatchOrderApiFactory : OrderApiFactory
{
    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("OrderProcessing:BatchSize", "1");
        builder.UseSetting("OrderProcessing:MaxOrdersPerRun", "3");
    }
}

public class BacklogDrainTests : IClassFixture<SmallBatchOrderApiFactory>
{
    private readonly SmallBatchOrderApiFactory _factory;
    private readonly HttpClient _client;

    public BacklogDrainTests(SmallBatchOrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_backlog_larger_than_one_batch_is_drained_within_the_budget()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await CreateOrderAsync()).Id);
        }

        // Batch size is 1 and the run budget is 3, so the first run drains three orders
        // across three batches and stops at the budget with a backlog remaining.
        var first = await ProcessAsync();
        first.Should().Be(3);

        var afterFirst = await CountPendingAsync();
        afterFirst.Should().Be(2);

        var second = await ProcessAsync();
        second.Should().Be(2);

        (await CountPendingAsync()).Should().Be(0);
        foreach (var id in ids)
        {
            (await GetStatusAsync(id)).Should().Be("PROCESSING");
        }
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

    private async Task<int> CountPendingAsync()
    {
        var pending = await _client.GetFromJsonAsync<List<OrderDto>>("/api/orders?status=PENDING");
        return pending!.Count;
    }

    private async Task<string> GetStatusAsync(Guid id)
    {
        var order = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{id}");
        return order!.Status;
    }
}
