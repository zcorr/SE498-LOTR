using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace LotrApi.Tests;

/// <summary>
/// Spins up PostgreSQL via Testcontainers and an API factory wired to that database.
/// </summary>
public sealed class LotrApiPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                    });
            });
        });

        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
