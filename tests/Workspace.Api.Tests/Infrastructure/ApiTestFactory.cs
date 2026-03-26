using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Workspace.Data;

namespace Workspace.Api.Tests.Infrastructure;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestAdminKey = "integration-test-admin-key";
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=unused",
                ["Admin:ApiKey"] = TestAdminKey
            };

            configBuilder.AddInMemoryCollection(inMemorySettings);
        });

        builder.ConfigureServices(services =>
        {
            var existingDbContext = services
                .SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (existingDbContext is not null)
            {
                services.Remove(existingDbContext);
            }

            _connection ??= new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
