namespace OrderProcessing.Infrastructure;

public sealed class OrderProcessingOptions
{
    public const string SectionName = "OrderProcessing";

    public string CronExpression { get; set; } = "*/5 * * * *";

    public int BatchSize { get; set; } = 200;
}
