using System.Net;

namespace Keycloak.Client;

public sealed class KeycloakClientException : Exception
{
    public KeycloakClientException(HttpStatusCode statusCode, string operation, string? responseBody)
        : base($"Keycloak operation '{operation}' failed with HTTP {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        Operation = operation;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }
    public string Operation { get; }
    public string? ResponseBody { get; }
}
