using Hangfire.Dashboard;

namespace OrderProcessing.Api.Dashboard;

public sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
