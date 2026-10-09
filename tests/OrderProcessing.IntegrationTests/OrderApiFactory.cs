using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace OrderProcessing.IntegrationTests;

public sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _container;

    public OrderApiFactory()
    {
        _container = new PostgreSqlBuilder("postgres:18-alpine")
            .Build();

        _container.StartAsync().GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
