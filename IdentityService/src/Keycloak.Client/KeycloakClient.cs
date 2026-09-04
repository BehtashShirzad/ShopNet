using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Keycloak.Client.Configuration;
using Keycloak.Client.Models;

namespace Keycloak.Client;

public sealed class KeycloakClient : IKeycloakClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _adminTokenLock = new(1, 1);
    private string? _adminAccessToken;
    private DateTimeOffset _adminAccessTokenExpiresAt;

    public KeycloakClient(HttpClient httpClient, KeycloakOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        options.Validate();

        _httpClient = httpClient;
        _options = options;
        _clock = clock;
    }

    public async Task<string> RegisterUserAsync(
        RegisterKeycloakUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.", nameof(request));

        var body = new
        {
            username = request.Email,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = request.Enabled,
            emailVerified = request.EmailVerified,
            credentials = new[]
            {
                new { type = "password", value = request.Password, temporary = false }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, AdminUsersPath())
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        using var response = await SendAdminAsync(message, "register user", cancellationToken);
        await EnsureSuccessAsync(response, "register user", cancellationToken);

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return the created user's location.");
        var userId = location.Segments.LastOrDefault()?.Trim('/');
        return !string.IsNullOrWhiteSpace(userId)
            ? Uri.UnescapeDataString(userId)
            : throw new InvalidOperationException("Keycloak returned an invalid user location.");
    }

    public Task<KeycloakTokenResponse> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var values = LoginClientValues();
        values["grant_type"] = "password";
        values["username"] = username;
        values["password"] = password;
        values["scope"] = "openid profile email";
        return RequestTokenAsync(values, "login", cancellationToken);
    }

    public Task<KeycloakTokenResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var values = LoginClientValues();
        values["grant_type"] = "refresh_token";
        values["refresh_token"] = refreshToken;
        return RequestTokenAsync(values, "refresh token", cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var values = LoginClientValues();
        values["refresh_token"] = refreshToken;
        using var content = new FormUrlEncodedContent(values);
        using var response = await _httpClient.PostAsync(LogoutPath(), content, cancellationToken);
        await EnsureSuccessAsync(response, "logout", cancellationToken);
    }

    public async Task LogoutUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{AdminUserPath(userId)}/logout");
        using var response = await SendAdminAsync(message, "logout user sessions", cancellationToken);
        await EnsureSuccessAsync(response, "logout user sessions", cancellationToken);
    }

    public async Task<KeycloakUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AdminUserPath(userId));
        using var response = await SendAdminAsync(message, "get user", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, "get user", cancellationToken);
        return await DeserializeAsync<KeycloakUser>(response, "get user", cancellationToken);
    }

    public async Task ResetPasswordAsync(
        string userId,
        string newPassword,
        bool temporary = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        using var message = new HttpRequestMessage(HttpMethod.Put, $"{AdminUserPath(userId)}/reset-password")
        {
            Content = JsonContent.Create(new { type = "password", value = newPassword, temporary }, options: JsonOptions)
        };
        using var response = await SendAdminAsync(message, "reset password", cancellationToken);
        await EnsureSuccessAsync(response, "reset password", cancellationToken);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, AdminUserPath(userId));
        using var response = await SendAdminAsync(message, "delete user", cancellationToken);
        await EnsureSuccessAsync(response, "delete user", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAdminAsync(
        HttpRequestMessage message,
        string operation,
        CancellationToken cancellationToken)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", await GetAdminAccessTokenAsync(cancellationToken));
        try
        {
            return await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Keycloak operation '{operation}' could not reach the server.", exception);
        }
    }

    private async Task<string> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (HasUsableAdminToken())
            return _adminAccessToken!;

        await _adminTokenLock.WaitAsync(cancellationToken);
        try
        {
            if (HasUsableAdminToken())
                return _adminAccessToken!;

            var token = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.AdminClientId,
                ["client_secret"] = _options.AdminClientSecret
            }, "admin token", cancellationToken);
            _adminAccessToken = token.AccessToken;
            _adminAccessTokenExpiresAt = _clock.GetUtcNow().AddSeconds(token.ExpiresIn);
            return token.AccessToken;
        }
        finally
        {
            _adminTokenLock.Release();
        }
    }

    private bool HasUsableAdminToken() =>
        !string.IsNullOrWhiteSpace(_adminAccessToken) &&
        _adminAccessTokenExpiresAt > _clock.GetUtcNow().AddSeconds(30);

    private async Task<KeycloakTokenResponse> RequestTokenAsync(
        Dictionary<string, string> values,
        string operation,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(values);
        using var response = await _httpClient.PostAsync(TokenPath(), content, cancellationToken);
        await EnsureSuccessAsync(response, operation, cancellationToken);
        return await DeserializeAsync<KeycloakTokenResponse>(response, operation, cancellationToken);
    }

    private Dictionary<string, string> LoginClientValues()
    {
        var values = new Dictionary<string, string> { ["client_id"] = _options.LoginClientId };
        if (!string.IsNullOrWhiteSpace(_options.LoginClientSecret))
            values["client_secret"] = _options.LoginClientSecret;
        return values;
    }

    private async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException($"Keycloak operation '{operation}' returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new KeycloakClientException(response.StatusCode, operation, body);
    }

    private string TokenPath() => $"{RealmPath()}/protocol/openid-connect/token";
    private string LogoutPath() => $"{RealmPath()}/protocol/openid-connect/logout";
    private string AdminUsersPath() => $"{BaseUrl()}/admin/realms/{Segment(_options.Realm)}/users";
    private string AdminUserPath(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return $"{AdminUsersPath()}/{Segment(userId)}";
    }
    private string RealmPath() => $"{BaseUrl()}/realms/{Segment(_options.Realm)}";
    private string BaseUrl() => _options.BaseUrl.TrimEnd('/');
    private static string Segment(string value) => Uri.EscapeDataString(value);

    public void Dispose() => _adminTokenLock.Dispose();
}
