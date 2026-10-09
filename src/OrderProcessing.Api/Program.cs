using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OrderProcessing.Api.ErrorHandling;
using OrderProcessing.Api.Health;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Application.Observability;
using OrderProcessing.Application.Validation;
using OrderProcessing.Infrastructure;
using OrderProcessing.Infrastructure.Persistence;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfireServer();

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var detail = string.Join("; ", context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage));

        return new ObjectResult(new
        {
            type = "https://example.com/problems/validation-error",
            title = "Validation failed",
            status = StatusCodes.Status400BadRequest,
            code = "VALIDATION_ERROR",
            detail = string.IsNullOrWhiteSpace(detail) ? "The request could not be read." : detail,
            traceId = context.HttpContext.TraceIdentifier
        })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateStatusRequest>, UpdateStatusRequestValidator>();
builder.Services.AddScoped<IValidator<ListOrdersQuery>, ListOrdersQueryValidator>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks().AddCheck<DbContextHealthCheck>("database");

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(OrderMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

await ApplyMigrationsAsync(app);

try
{
    app.Services.RegisterRecurringJobs();
}
catch (Exception exception)
{
    app.Logger.LogWarning(exception, "Recurring jobs could not be registered at startup.");
}

app.Use(async (context, next) =>
{
    using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
    {
        await next();
    }
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await using var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    await using (var lockCommand = connection.CreateCommand())
    {
        lockCommand.CommandText = "SELECT pg_advisory_lock(727274)";
        await lockCommand.ExecuteNonQueryAsync();
    }

    try
    {
        await dbContext.Database.MigrateAsync();
    }
    finally
    {
        await using var unlockCommand = connection.CreateCommand();
        unlockCommand.CommandText = "SELECT pg_advisory_unlock(727274)";
        await unlockCommand.ExecuteNonQueryAsync();
    }
}

public partial class Program;
