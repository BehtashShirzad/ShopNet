using System.Net;
using System.Text;
using ShopNet.Authorization;

namespace BuildingBlocks.UnitTests.Authorization;

public sealed class ServiceAccessTokenProviderTests
{
    [Fact]
    public async Task GetToken_CachesUsableToken()
    {
        var handler = new TokenHandler();
        using var provider = new ServiceAccessTokenProvider(
            new HttpClient(handler),
            new ServiceClientOptions("http://keycloak/token", "cart-service", "secret"),
            TimeProvider.System);

        var first = await provider.GetTokenAsync();
        var second = await provider.GetTokenAsync();

        Assert.Equal("service-token", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("client_credentials", handler.Form["grant_type"]);
        Assert.Equal("cart-service", handler.Form["client_id"]);
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Dictionary<string, string> Form { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Form = body.Split('&').Select(value => value.Split('='))
                .ToDictionary(value => Uri.UnescapeDataString(value[0]),
                    value => Uri.UnescapeDataString(value[1]));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"service-token\",\"expires_in\":300}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
