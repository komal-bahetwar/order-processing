using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Idempotency;
using OrderProcessing.Application.Services;

namespace OrderProcessing.IntegrationTests;

public class IdempotencyTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public IdempotencyTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_sequential_retry_with_the_same_key_replays_the_original_response()
    {
        var key = Guid.NewGuid().ToString("N");
        var request = Invoice();

        var first = await PostAsync(_client, request, key);
        var second = await PostAsync(_client, request, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstOrder = (await first.Content.ReadFromJsonAsync<OrderDto>())!;
        var secondOrder = (await second.Content.ReadFromJsonAsync<OrderDto>())!;
        secondOrder.Id.Should().Be(firstOrder.Id);
        secondOrder.TotalAmount.Should().Be(firstOrder.TotalAmount);
    }

    [Fact]
    public async Task A_different_payload_under_the_same_key_is_rejected()
    {
        var key = Guid.NewGuid().ToString("N");

        var first = await PostAsync(_client, Invoice(10m), key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await PostAsync(_client, Invoice(99m), key);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task A_request_without_a_key_creates_a_new_order_each_time()
    {
        var first = await PostAsync(_client, Invoice(), key: null);
        var second = await PostAsync(_client, Invoice(), key: null);

        var firstOrder = (await first.Content.ReadFromJsonAsync<OrderDto>())!;
        var secondOrder = (await second.Content.ReadFromJsonAsync<OrderDto>())!;
        secondOrder.Id.Should().NotBe(firstOrder.Id);
    }

    [Theory]
    [InlineData("bad key!")]
    [InlineData("with/slash")]
    public async Task A_malformed_key_is_rejected(string key)
    {
        var response = await PostAsync(_client, Invoice(), key);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_key_longer_than_one_hundred_and_twenty_eight_characters_is_rejected()
    {
        var response = await PostAsync(_client, Invoice(), new string('a', 129));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_repeated_key_header_is_rejected()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(Invoice())
        };
        message.Headers.TryAddWithoutValidation(IdempotencyKey.HeaderName, new[] { "one", "two" });

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Different_keys_create_independent_orders()
    {
        var first = await PostAsync(_client, Invoice(), Guid.NewGuid().ToString("N"));
        var second = await PostAsync(_client, Invoice(), Guid.NewGuid().ToString("N"));

        var firstOrder = (await first.Content.ReadFromJsonAsync<OrderDto>())!;
        var secondOrder = (await second.Content.ReadFromJsonAsync<OrderDto>())!;
        secondOrder.Id.Should().NotBe(firstOrder.Id);
    }

    [Fact]
    public async Task A_failed_create_does_not_claim_the_key()
    {
        var key = Guid.NewGuid().ToString("N");

        var invalid = await PostAsync(_client, new CreateOrderRequest([]), key);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var valid = await PostAsync(_client, Invoice(), key);
        valid.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_retry_after_the_order_moved_on_replays_the_original_snapshot()
    {
        var key = Guid.NewGuid().ToString("N");
        var request = Invoice();

        var first = await PostAsync(_client, request, key);
        var firstOrder = (await first.Content.ReadFromJsonAsync<OrderDto>())!;

        using (var scope = _factory.Services.CreateScope())
        {
            var processing = scope.ServiceProvider.GetRequiredService<IOrderProcessingService>();
            await processing.ProcessPendingOrdersAsync();
        }

        var moved = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{firstOrder.Id}");
        moved!.Status.Should().Be("PROCESSING");

        var retry = await PostAsync(_client, request, key);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        var replayed = (await retry.Content.ReadFromJsonAsync<OrderDto>())!;
        replayed.Id.Should().Be(firstOrder.Id);
        replayed.Status.Should().Be("PENDING");
    }

    [Fact]
    public async Task A_present_but_blank_key_is_rejected()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(Invoice())
        };
        message.Headers.TryAddWithoutValidation(IdempotencyKey.HeaderName, " ");

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_create_exactly_one_order()
    {
        using var factory = new OrderApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var key = Guid.NewGuid().ToString("N");
        var request = Invoice();

        var responses = await Task.WhenAll(
            PostAsync(clientA, request, key),
            PostAsync(clientB, request, key));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);

        var ids = new List<Guid>();
        foreach (var response in responses)
        {
            ids.Add((await response.Content.ReadFromJsonAsync<OrderDto>())!.Id);
        }

        ids.Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Cleanup_removes_expired_records_and_keeps_live_ones()
    {
        using var factory = new OrderApiFactory();
        _ = factory.CreateClient(); // boot the host
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        unitOfWork.Idempotency.Add(new IdempotencyRecord(
            "cleanup-test/v1", "expired", "fp", Guid.NewGuid(), "{}", "application/json", 201, "/x",
            now.AddHours(-25), now.AddHours(-1)));
        unitOfWork.Idempotency.Add(new IdempotencyRecord(
            "cleanup-test/v1", "live", "fp", Guid.NewGuid(), "{}", "application/json", 201, "/x",
            now, now.AddHours(24)));
        await unitOfWork.SaveChangesAsync();

        var removed = await unitOfWork.Idempotency.DeleteExpiredAsync(now);

        removed.Should().Be(1);
        var live = await unitOfWork.Idempotency.FindAsync("cleanup-test/v1", "live");
        live.Should().NotBeNull();
    }

    private static CreateOrderRequest Invoice(decimal unitPrice = 10m) =>
        new([new CreateOrderItemRequest(Guid.NewGuid(), 1, unitPrice)]);

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        CreateOrderRequest request,
        string? key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(request)
        };

        if (key is not null)
        {
            message.Headers.TryAddWithoutValidation(IdempotencyKey.HeaderName, key);
        }

        return await client.SendAsync(message);
    }
}
