using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityService.Configuration;
using Microsoft.Extensions.Configuration;

namespace IdentityService.UnitTests.Configuration;

public sealed class OpenIddictCertificateLoaderTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"shopnet-identity-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Load_WithoutConfiguredPath_ThrowsClearError()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => OpenIddictCertificateLoader.Load(configuration));

        Assert.Contains("OpenIddict:Certificate:Path", exception.Message);
    }

    [Fact]
    public void Load_WithValidPfx_ReturnsCertificateWithPrivateKey()
    {
        const string password = "test-password";
        Directory.CreateDirectory(_temporaryDirectory);
        var certificatePath = Path.Combine(_temporaryDirectory, "openiddict.pfx");
        File.WriteAllBytes(certificatePath, CreatePfx(password));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Certificate:Path"] = certificatePath,
                ["OpenIddict:Certificate:Password"] = password
            })
            .Build();

        using var certificate = OpenIddictCertificateLoader.Load(configuration);

        Assert.True(certificate.HasPrivateKey);
        Assert.Equal("CN=ShopNet Identity Test", certificate.Subject);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static byte[] CreatePfx(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=ShopNet Identity Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        return certificate.Export(X509ContentType.Pfx, password);
    }
}
