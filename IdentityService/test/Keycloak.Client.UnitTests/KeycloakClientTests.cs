using System.Net;
using System.Text;
using Keycloak.Client.Configuration;
using Keycloak.Client.Models;

namespace Keycloak.Client.UnitTests;

public sealed class KeycloakClientTests
{
    [Fact]
    public async Task RegisterUser_GetsAdminTokenAndCreatesEnabledUser()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, "{\"access_token\":\"admin-token\",\"expires_in\":300}"),
            Response(HttpStatusCode.Created, response =>
                response.Headers.Location = new Uri("http://keycloak/admin/realms/shopnet/users/user-123")));
        using var client = CreateClient(handler);

        var id = await client.RegisterUserAsync(new RegisterKeycloakUserRequest(
            "user@example.com", "Password1!", "First", "Last"));

        Assert.Equal("user-123", id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/realms/shopnet/protocol/openid-connect/token", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Contains("grant_type=client_credentials", handler.Requests[0].Body);
        Assert.EndsWith("/admin/realms/shopnet/users", handler.Requests[1].Uri.AbsoluteUri);
        Assert.Equal("Bearer", handler.Requests[1].AuthorizationScheme);
        Assert.Equal("admin-token", handler.Requests[1].AuthorizationParameter);
        Assert.Contains("\"username\":\"user@example.com\"", handler.Requests[1].Body);
        Assert.Contains("\"temporary\":false", handler.Requests[1].Body);
    }

    [Fact]
    public async Task Login_UsesPasswordGrantAndReturnsTokens()
    {
        var handler = new RecordingHandler(Json(HttpStatusCode.OK,
            "{\"access_token\":\"access\",\"refresh_token\":\"refresh\",\"expires_in\":60}"));
        using var client = CreateClient(handler);

        var token = await client.LoginAsync("user@example.com", "Password1!");

        Assert.Equal("access", token.AccessToken);
        Assert.Equal("refresh", token.RefreshToken);
        Assert.Contains("grant_type=password", handler.Requests.Single().Body);
        Assert.Contains("client_id=shopnet-web", handler.Requests.Single().Body);
        Assert.Contains("username=user%40example.com", handler.Requests.Single().Body);
    }

    [Fact]
    public async Task RefreshAndLogout_UseOidcEndpoints()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, "{\"access_token\":\"new-access\",\"expires_in\":60}"),
            Response(HttpStatusCode.NoContent));
        using var client = CreateClient(handler);

        var token = await client.RefreshTokenAsync("refresh-token");
        await client.LogoutAsync("refresh-token");

        Assert.Equal("new-access", token.AccessToken);
        Assert.Contains("grant_type=refresh_token", handler.Requests[0].Body);
        Assert.EndsWith("/protocol/openid-connect/logout", handler.Requests[1].Uri.AbsoluteUri);
        Assert.Contains("refresh_token=refresh-token", handler.Requests[1].Body);
    }

    [Fact]
    public async Task GetResetLogoutAndDelete_UseAdminUserEndpointsAndReuseToken()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, "{\"access_token\":\"admin-token\",\"expires_in\":300}"),
            Json(HttpStatusCode.OK, "{\"id\":\"user-1\",\"email\":\"user@example.com\",\"enabled\":true}"),
            Response(HttpStatusCode.NoContent),
            Response(HttpStatusCode.NoContent),
            Response(HttpStatusCode.NoContent));
        using var client = CreateClient(handler);

        var user = await client.GetUserAsync("user-1");
        await client.ResetPasswordAsync("user-1", "NewPassword1!");
        await client.LogoutUserSessionsAsync("user-1");
        await client.DeleteUserAsync("user-1");

        Assert.Equal("user@example.com", user?.Email);
        Assert.Equal(5, handler.Requests.Count);
        Assert.Equal(HttpMethod.Put, handler.Requests[2].Method);
        Assert.Contains("\"value\":\"NewPassword1!\"", handler.Requests[2].Body);
        Assert.EndsWith("/users/user-1/logout", handler.Requests[3].Uri.AbsoluteUri);
        Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
    }

    [Fact]
    public async Task FailedResponse_ThrowsExceptionWithStatusAndBody()
    {
        var handler = new RecordingHandler(Json(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}"));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<KeycloakClientException>(
            () => client.LoginAsync("user@example.com", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("invalid_grant", exception.ResponseBody);
    }

    private static global::Keycloak.Client.KeycloakClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new KeycloakOptions
        {
            BaseUrl = "http://keycloak:8080",
            Realm = "shopnet",
            AdminClientId = "identity-admin-service",
            AdminClientSecret = "admin-secret",
            LoginClientId = "shopnet-web"
        }, TimeProvider.System);

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        Action<HttpResponseMessage>? configure = null)
    {
        var response = new HttpResponseMessage(statusCode);
        configure?.Invoke(response);
        return response;
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri!,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter));
            return _responses.Dequeue();
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri Uri,
        string Body,
        string? AuthorizationScheme,
        string? AuthorizationParameter);
}
