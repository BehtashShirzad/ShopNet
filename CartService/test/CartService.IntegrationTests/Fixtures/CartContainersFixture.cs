using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Testcontainers.MsSql;

namespace CartService.IntegrationTests;

public sealed class CartContainersFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:7.0-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-alpine").Build();
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    public string SqlConnectionString => _sql.GetConnectionString();

    public string RedisConnectionString => _redis.GetConnectionString();
    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public Task InitializeAsync() => Task.WhenAll(
        _redis.StartAsync(),
        _rabbitMq.StartAsync(), _sql.StartAsync());

    public async Task DisposeAsync()
    {
        await _rabbitMq.DisposeAsync();
        await _redis.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class CartContainersCollection : ICollectionFixture<CartContainersFixture>
{
    public const string Name = "Cart containers";
}
