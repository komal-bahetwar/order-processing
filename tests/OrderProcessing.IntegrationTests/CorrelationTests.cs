using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace OrderProcessing.IntegrationTests;

public class CorrelationTests : IClassFixture<OrderApiFactory>
{
    private static readonly Regex Allowed = new("^[A-Za-z0-9._-]{1,64}$");

    private readonly HttpClient _client;

    public CorrelationTests(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_generated_correlation_id_is_returned_when_absent()
    {
        var response = await _client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var correlationId = SingleCorrelationId(response);
        Allowed.IsMatch(correlationId).Should().BeTrue(correlationId);
    }

    [Fact]
    public async Task A_valid_correlation_id_is_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "abc-123_XYZ.9");

        var response = await _client.SendAsync(request);

        SingleCorrelationId(response).Should().Be("abc-123_XYZ.9");
    }

    [Theory]
    [InlineData("bad header!")]
    [InlineData("with/slash")]
    public async Task An_invalid_correlation_id_is_rejected_with_a_generated_header(string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", value);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var correlationId = SingleCorrelationId(response);
        Allowed.IsMatch(correlationId).Should().BeTrue(correlationId);
        correlationId.Should().NotBe(value);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task A_correlation_id_longer_than_sixty_four_characters_is_rejected()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", new string('a', 65));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Allowed.IsMatch(SingleCorrelationId(response)).Should().BeTrue();
    }

    [Fact]
    public async Task A_repeated_correlation_header_is_rejected()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", new[] { "one", "two" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Allowed.IsMatch(SingleCorrelationId(response)).Should().BeTrue();
    }

    [Fact]
    public async Task An_error_response_carries_a_correlation_header_and_a_trace_id()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Allowed.IsMatch(SingleCorrelationId(response)).Should().BeTrue();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("ORDER_NOT_FOUND");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static string SingleCorrelationId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        var list = values!.ToList();
        list.Should().ContainSingle();
        return list[0];
    }
}
