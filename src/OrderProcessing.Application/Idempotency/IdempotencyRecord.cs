namespace OrderProcessing.Application.Idempotency;

public sealed class IdempotencyRecord
{
    public const string CreateOrderScope = "create-order/v1";

    private IdempotencyRecord()
    {
    }

    public IdempotencyRecord(
        string scope,
        string key,
        string requestFingerprint,
        Guid orderId,
        string responseBody,
        string contentType,
        int statusCode,
        string location,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        Scope = scope;
        Key = key;
        RequestFingerprint = requestFingerprint;
        OrderId = orderId;
        ResponseBody = responseBody;
        ContentType = contentType;
        StatusCode = statusCode;
        Location = location;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public string Scope { get; private set; } = null!;

    public string Key { get; private set; } = null!;

    public string RequestFingerprint { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public string ResponseBody { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public int StatusCode { get; private set; }

    public string Location { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
}
