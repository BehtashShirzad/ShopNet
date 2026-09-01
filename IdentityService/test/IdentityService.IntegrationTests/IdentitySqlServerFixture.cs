using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace IdentityService.IntegrationTests;

public sealed class IdentitySqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(
            _sqlServer.GetConnectionString())
        {
            InitialCatalog = $"IdentityTests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    public Task DisposeAsync() => _sqlServer.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class IdentitySqlServerCollection : ICollectionFixture<IdentitySqlServerFixture>
{
    public const string Name = "Identity SQL Server";
}
