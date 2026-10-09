using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OrderProcessing.Application.Dtos;

namespace OrderProcessing.Application.Idempotency;

public static class IdempotencyRequestFingerprint
{
    /// <summary>
    /// A deterministic fingerprint of the normalized create request. It preserves
    /// item order, duplicate lines, quantities, product ids, and normalized
    /// prices, and ignores JSON whitespace and property ordering.
    /// </summary>
    public static string Compute(CreateOrderRequest request)
    {
        var builder = new StringBuilder();

        foreach (var item in request.Items)
        {
            var price = decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero);
            builder
                .Append(item.ProductId.ToString("N"))
                .Append(':')
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(price.ToString("F2", CultureInfo.InvariantCulture))
                .Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }
}
