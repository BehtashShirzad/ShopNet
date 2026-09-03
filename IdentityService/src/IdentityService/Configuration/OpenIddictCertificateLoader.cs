using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IdentityService.Configuration;

public static class OpenIddictCertificateLoader
{
    private const string CertificatePathKey = "OpenIddict:Certificate:Path";
    private const string CertificatePasswordKey = "OpenIddict:Certificate:Password";

    public static X509Certificate2 Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredPath = configuration[CertificatePathKey];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"A production OpenIddict certificate is required. Configure '{CertificatePathKey}'.");
        }

        var certificatePath = Path.GetFullPath(configuredPath);
        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException(
                $"The configured OpenIddict certificate was not found at '{certificatePath}'.");
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                configuration[CertificatePasswordKey],
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "The configured OpenIddict certificate could not be loaded. Check the PFX file and password.",
                exception);
        }
    }
}
