namespace Keycloak.Client.Configuration;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string BaseUrl { get; init; } = string.Empty;
    public string Realm { get; init; } = string.Empty;
    public string AdminClientId { get; init; } = string.Empty;
    public string AdminClientSecret { get; init; } = string.Empty;
    public string LoginClientId { get; init; } = string.Empty;
    public string? LoginClientSecret { get; init; }

    internal void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("Keycloak:BaseUrl must be an absolute URL.");
        if (string.IsNullOrWhiteSpace(Realm))
            throw new InvalidOperationException("Keycloak:Realm is required.");
        if (string.IsNullOrWhiteSpace(AdminClientId))
            throw new InvalidOperationException("Keycloak:AdminClientId is required.");
        if (string.IsNullOrWhiteSpace(AdminClientSecret))
            throw new InvalidOperationException("Keycloak:AdminClientSecret is required.");
        if (string.IsNullOrWhiteSpace(LoginClientId))
            throw new InvalidOperationException("Keycloak:LoginClientId is required.");
    }
}
