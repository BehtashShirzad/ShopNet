using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Keycloak.Client;
using Keycloak.Client.Configuration;

namespace IdentityService.IntegrationTests.Fixtures;

public sealed class KeycloakFixture : IAsyncLifetime
{
    private const ushort KeycloakPort = 8080;
    private readonly IContainer _container;

    public KeycloakFixture()
    {
        var realmFile = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../../deploy/keycloak/shopnet-realm.json"));

        _container = new ContainerBuilder("quay.io/keycloak/keycloak:26.7.3")
            .WithImagePullPolicy(PullPolicy.Never)
            .WithPortBinding(KeycloakPort, true)
            .WithBindMount(realmFile, "/opt/keycloak/data/import/shopnet-realm.json")
            .WithCommand("start-dev", "--import-realm")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(KeycloakPort).ForPath("/realms/shopnet")))
            .Build();
    }

    public KeycloakClient CreateClient() => new(
        new HttpClient(),
        new KeycloakOptions
        {
            BaseUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(KeycloakPort)}",
            Realm = "shopnet",
            AdminClientId = "identity-admin-service",
            AdminClientSecret = "ShopNet!KeycloakClient2026",
            LoginClientId = "shopnet-web",
            Audience = "shopnet-api"
        },
        TimeProvider.System);

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class KeycloakCollection : ICollectionFixture<KeycloakFixture>
{
    public const string Name = "Keycloak";
}
