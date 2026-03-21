namespace Microsoft.Extensions.Hosting;

/// <summary>Supported authentication provider types for the Bethuya platform.</summary>
public enum AuthProviderType
{
    /// <summary>No authentication — development placeholder. All users treated as authenticated admin.</summary>
    None = 0,

    /// <summary>Microsoft Entra External ID (formerly Azure AD B2C).</summary>
    Entra,

    /// <summary>Auth0 by Okta.</summary>
    Auth0,

    /// <summary>Keycloak (self-hosted OpenID Connect).</summary>
    Keycloak
}
