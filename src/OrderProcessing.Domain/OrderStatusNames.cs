namespace OrderProcessing.Domain;

/// <summary>
/// Parses order status strings by their defined names only, case-insensitively.
/// Numeric strings such as "999" are rejected even though <see cref="Enum.TryParse{T}"/>
/// would accept them as an out-of-range underlying value.
/// </summary>
public static class OrderStatusNames
{
    private static readonly Dictionary<string, OrderStatus> ByName =
        Enum.GetValues<OrderStatus>()
            .ToDictionary(status => status.ToString(), status => status, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Names { get; } = ByName.Keys.ToArray();

    public static bool TryParse(string? value, out OrderStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return ByName.TryGetValue(value.Trim(), out status);
    }
}
