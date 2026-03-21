namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Configuration options for Bethuya authentication.
/// Bind from the <c>Authentication</c> section in appsettings.json.
/// </summary>
public sealed class BethuyaAuthOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Which OIDC provider to use. Defaults to <see cref="AuthProviderType.None"/> (dev mode).</summary>
    public AuthProviderType Provider { get; set; } = AuthProviderType.None;

    /// <summary>Entra External ID settings. Used when <see cref="Provider"/> is <see cref="AuthProviderType.Entra"/>.</summary>
    public EntraOptions Entra { get; set; } = new();

    /// <summary>Auth0 settings. Used when <see cref="Provider"/> is <see cref="AuthProviderType.Auth0"/>.</summary>
    public Auth0Options Auth0 { get; set; } = new();

    /// <summary>Keycloak settings. Used when <see cref="Provider"/> is <see cref="AuthProviderType.Keycloak"/>.</summary>
    public KeycloakOptions Keycloak { get; set; } = new();
}

/// <summary>Microsoft Entra External ID configuration.</summary>
public sealed class EntraOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>The Entra External ID domain (e.g., "yourtenant.ciamlogin.com").</summary>
    public string Domain { get; set; } = "";
}

/// <summary>Auth0 configuration.</summary>
public sealed class Auth0Options
{
    public string Domain { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Audience { get; set; } = "";
    public string CallbackPath { get; set; } = "/callback";
}

/// <summary>Keycloak configuration.</summary>
public sealed class KeycloakOptions
{
    /// <summary>The Keycloak authority URL (e.g., "https://keycloak.example.com/realms/bethuya").</summary>
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackPath { get; set; } = "/signin-oidc";
}
