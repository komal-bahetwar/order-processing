using System.Diagnostics.Metrics;

namespace OrderProcessing.Application.Observability;

public static class OrderMetrics
{
    public const string MeterName = "OrderProcessing";

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Transitions = Meter.CreateCounter<long>(
        "orders.transitions",
        unit: "{transition}",
        description: "The number of order status transitions, tagged with the from and to status.");
}
