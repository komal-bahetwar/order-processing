using System.Globalization;
using System.Text;

namespace OrderProcessing.Application.Pagination;

/// <summary>
/// An opaque keyset cursor over the stable (CreatedAt, Id) ordering. It encodes the
/// position of the last item on the previous page and is never interpreted as SQL.
/// </summary>
public static class OrderCursor
{
    public const int MaxLength = 128;

    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > MaxLength)
        {
            return false;
        }

        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);

            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[1], "N", out id))
            {
                return false;
            }

            if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return false;
            }

            createdAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
