using Microsoft.Extensions.DependencyInjection;
using ShopNet.Authorization;
using Swashbuckle.AspNetCore.Swagger;

namespace BuildingBlocks.UnitTests.Authorization;

public sealed class SwaggerAuthorizationExtensionsTests
{
    [Fact]
    public void AddShopNetSwagger_RegistersSwaggerProvider()
    {
        var services = new ServiceCollection();

        services.AddShopNetSwagger("Test Service");

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISwaggerProvider));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddShopNetSwagger_WithInvalidTitle_Throws(string? title)
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() => services.AddShopNetSwagger(title!));
    }
}
