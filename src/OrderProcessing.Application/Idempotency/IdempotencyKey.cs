namespace OrderProcessing.Application.Idempotency;

public static class IdempotencyKey
{
    public const string HeaderName = "Idempotency-Key";

    public const int MaxLength = 128;

    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length <= MaxLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
