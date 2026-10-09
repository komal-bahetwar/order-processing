using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Domain;

namespace OrderProcessing.IntegrationTests;

public class PaginationTests
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_dataset_larger_than_one_hundred_is_traversed_completely()
    {
        using var factory = new OrderApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory, 105);

        var ids = await TraverseAsync(client, status: null, limit: 20);

        ids.Should().HaveCount(105);
        ids.Distinct().Should().HaveCount(105);
    }

    [Fact]
    public async Task Orders_with_the_same_timestamp_are_traversed_without_duplicates_or_gaps()
    {
        using var factory = new OrderApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory, 5, sameTime: Base);

        var ids = await TraverseAsync(client, status: null, limit: 2);

        ids.Should().HaveCount(5);
        ids.Distinct().Should().HaveCount(5);
    }

    [Fact]
    public async Task The_final_page_omits_the_next_cursor_header()
    {
        using var factory = new OrderApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory, 3);

        var response = await client.GetAsync("/api/orders?limit=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-Next-Cursor").Should().BeFalse();
    }

    [Fact]
    public async Task An_invalid_cursor_returns_bad_request()
    {
        using var factory = new OrderApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orders?cursor=not-a-real-cursor");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_cursor_with_out_of_range_ticks_returns_bad_request()
    {
        using var factory = new OrderApiFactory();
        var client = factory.CreateClient();
        var cursor = EncodeCursor($"-1|{Guid.NewGuid():N}");

        var response = await client.GetAsync($"/api/orders?cursor={Uri.EscapeDataString(cursor)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string EncodeCursor(string raw) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    [Fact]
    public async Task A_filter_is_consistent_across_pages()
    {
        using var factory = new OrderApiFactory();
        var client = factory.CreateClient();
        await SeedAsync(factory, 5, status: OrderStatus.Pending);
        await SeedAsync(factory, 3, status: OrderStatus.Cancelled, startOffset: 100);

        var ids = await TraverseAsync(client, status: "PENDING", limit: 2);

        ids.Should().HaveCount(5);
        ids.Distinct().Should().HaveCount(5);
    }

    private static async Task<List<Guid>> TraverseAsync(HttpClient client, string? status, int limit)
    {
        var ids = new List<Guid>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var url = $"/api/orders?limit={limit}"
                + (status is null ? string.Empty : $"&status={status}")
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");

            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var page = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
            ids.AddRange(page!.Select(order => order.Id));

            cursor = response.Headers.TryGetValues("X-Next-Cursor", out var values) ? values.Single() : null;
            pages++;
        }
        while (cursor is not null && pages < 1000);

        return ids;
    }

    private static async Task SeedAsync(
        OrderApiFactory factory,
        int count,
        DateTimeOffset? sameTime = null,
        OrderStatus status = OrderStatus.Pending,
        int startOffset = 0)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        for (var i = 0; i < count; i++)
        {
            var time = sameTime ?? Base.AddSeconds(startOffset + i);
            var order = Order.Create([new OrderItem(Guid.NewGuid(), 1, 10m)], time);
            if (status == OrderStatus.Cancelled)
            {
                order.Cancel(time);
            }

            unitOfWork.Orders.Add(order);
        }

        await unitOfWork.SaveChangesAsync();
    }
}
