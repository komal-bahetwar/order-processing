using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Services;
using OrderProcessing.Infrastructure.Jobs;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure;

public static class DependencyInjection
{
    public const string RecurringJobId = "process-pending-orders";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.Configure<OrderProcessingOptions>(
            configuration.GetSection(OrderProcessingOptions.SectionName));

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderProcessingService, OrderProcessingService>();

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        return services;
    }

    public static void RegisterRecurringJobs(this IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<OrderProcessingOptions>>().Value;

        RecurringJob.AddOrUpdate<ProcessPendingOrdersJob>(
            RecurringJobId,
            job => job.RunAsync(CancellationToken.None),
            options.CronExpression);
    }
}
