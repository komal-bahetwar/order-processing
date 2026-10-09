using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Domain;

namespace OrderProcessing.IntegrationTests;

public class ApiHardeningTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public ApiHardeningTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_with_a_null_element_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders", new { items = new object?[] { null } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_above_the_amount_ceiling_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { productId = Guid.NewGuid(), quantity = 1, unitPrice = 10_000_000_000_000_000m } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_extreme_amounts_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new { items = new[] { new { productId = Guid.NewGuid(), quantity = int.MaxValue, unitPrice = decimal.MaxValue } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_with_an_undefined_status_name_returns_bad_request()
    {
        var response = await _client.GetAsync("/api/orders?status=999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_status_with_an_undefined_name_returns_bad_request()
    {
        var order = await CreateOrderAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/status", new UpdateStatusRequest("999"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_fresh_context_exposes_a_genuinely_read_only_collection()
    {
        var order = await CreateOrderAsync();

        using var scope = _factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loaded = await unitOfWork.Orders.GetByIdAsync(order.Id);

        loaded!.Items.Should().HaveCount(1);
        loaded.Items.Should().BeAssignableTo<ReadOnlyCollection<OrderItem>>();
        (loaded.Items as List<OrderItem>).Should().BeNull();

        var collection = (ICollection<OrderItem>)loaded.Items;
        ((Action)(() => collection.Clear())).Should().Throw<NotSupportedException>();
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
