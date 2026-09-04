using System.Text.Json.Serialization;

namespace Keycloak.Client.Models;

public sealed record RegisterKeycloakUserRequest(
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null,
    bool EmailVerified = false,
    bool Enabled = true);

public sealed record KeycloakTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

public sealed record KeycloakUser
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; init; }
}
