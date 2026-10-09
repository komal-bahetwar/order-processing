namespace OrderProcessing.Domain;

public static class Money
{
    // numeric(18,2): sixteen integer digits and two decimals.
    public const decimal MaxStoredAmount = 9_999_999_999_999_999.99m;

    public static decimal NormalizePrice(decimal price)
    {
        if (price < 0m)
        {
            throw new DomainException("INVALID_ITEM_PRICE", "Item unit price must not be negative.");
        }

        var normalized = decimal.Round(price, 2, MidpointRounding.AwayFromZero);

        if (normalized > MaxStoredAmount)
        {
            throw new DomainException(
                "AMOUNT_OUT_OF_RANGE",
                "The unit price exceeds the maximum supported amount.");
        }

        return normalized;
    }

    public static decimal LineTotal(int quantity, decimal normalizedUnitPrice)
    {
        if (normalizedUnitPrice != 0m && quantity > MaxStoredAmount / normalizedUnitPrice)
        {
            throw new DomainException(
                "AMOUNT_OUT_OF_RANGE",
                "The line total exceeds the maximum supported amount.");
        }

        return decimal.Round(quantity * normalizedUnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal AddChecked(decimal runningTotal, decimal lineTotal)
    {
        if (lineTotal > MaxStoredAmount - runningTotal)
        {
            throw new DomainException(
                "AMOUNT_OUT_OF_RANGE",
                "The order total exceeds the maximum supported amount.");
        }

        return runningTotal + lineTotal;
    }
}
