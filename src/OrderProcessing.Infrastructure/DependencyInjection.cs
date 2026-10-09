using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderProcessing.Application;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Services;
using OrderProcessing.Infrastructure.Jobs;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure;

public static class DependencyInjection
{
    public const string RecurringJobId = "process-pending-orders";

    public const string IdempotencyCleanupJobId = "idempotency-cleanup";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddOptions<OrderProcessingOptions>()
            .Bind(configuration.GetSection(OrderProcessingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OrderProcessingOptions>, OrderProcessingOptionsValidator>();

        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
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
        using var scope = serviceProvider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<OrderProcessingOptions>>().Value;
        var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobs.AddOrUpdate<ProcessPendingOrdersJob>(
            RecurringJobId,
            job => job.RunAsync(CancellationToken.None),
            options.CronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        recurringJobs.AddOrUpdate<IdempotencyCleanupJob>(
            IdempotencyCleanupJobId,
            job => job.RunAsync(CancellationToken.None),
            options.IdempotencyCleanupCronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
