using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace OrderService.IntegrationTests;

public sealed class OrderContainersFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(
        "rabbitmq:3.13-alpine").Build();

    public string DatabaseConnectionString { get; private set; } = string.Empty;
    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlServer.StartAsync(), _rabbitMq.StartAsync());
        DatabaseConnectionString = new SqlConnectionStringBuilder(
            _sqlServer.GetConnectionString())
        {
            InitialCatalog = $"OrderTests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        await _rabbitMq.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class OrderContainersCollection : ICollectionFixture<OrderContainersFixture>
{
    public const string Name = "Order containers";
}
