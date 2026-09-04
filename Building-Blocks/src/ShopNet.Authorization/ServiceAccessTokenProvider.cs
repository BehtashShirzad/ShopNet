using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShopNet.Authorization;

public interface IServiceAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

internal sealed class ServiceAccessTokenProvider(
    HttpClient httpClient, ServiceClientOptions options, TimeProvider clock)
    : IServiceAccessTokenProvider, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsUsable()) return _token!;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsUsable()) return _token!;
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret
            });
            using var response = await httpClient.PostAsync(options.TokenEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<ServiceToken>(cancellationToken)
                ?? throw new InvalidOperationException("Keycloak returned an empty service token response.");
            _token = token.AccessToken;
            _expiresAt = clock.GetUtcNow().AddSeconds(token.ExpiresIn);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsUsable() =>
        !string.IsNullOrWhiteSpace(_token) && _expiresAt > clock.GetUtcNow().AddSeconds(30);

    public void Dispose() => _lock.Dispose();

    private sealed record ServiceToken(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

internal sealed record ServiceClientOptions(string TokenEndpoint, string ClientId, string ClientSecret);

public static class ServiceAuthenticationExtensions
{
    public static IServiceCollection AddShopNetServiceAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetRequiredSection("Keycloak:ServiceClient");
        var tokenEndpoint = section["TokenEndpoint"]
            ?? throw new InvalidOperationException("Keycloak:ServiceClient:TokenEndpoint is required.");
        var clientId = section["ClientId"]
            ?? throw new InvalidOperationException("Keycloak:ServiceClient:ClientId is required.");
        var clientSecret = section["ClientSecret"]
            ?? throw new InvalidOperationException("Keycloak:ServiceClient:ClientSecret is required.");

        services.AddSingleton(new ServiceClientOptions(tokenEndpoint, clientId, clientSecret));
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IServiceAccessTokenProvider, ServiceAccessTokenProvider>();
        return services;
    }
}
