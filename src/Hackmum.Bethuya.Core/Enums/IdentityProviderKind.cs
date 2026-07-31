namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// External and platform identity providers that can be linked to a member.
/// </summary>
public enum IdentityProviderKind
{
    /// <summary>
    /// Native Bethuya platform identity.
    /// </summary>
    Platform,
    /// <summary>
    /// GitHub identity provider.
    /// </summary>
    GitHub,
    /// <summary>
    /// LinkedIn identity provider.
    /// </summary>
    LinkedIn,
    /// <summary>
    /// Microsoft Entra identity provider.
    /// </summary>
    MicrosoftEntra,
    /// <summary>
    /// Auth0 identity provider.
    /// </summary>
    Auth0,
    /// <summary>
    /// Keycloak identity provider.
    /// </summary>
    Keycloak,
    /// <summary>
    /// Discord identity provider.
    /// </summary>
    Discord,
    /// <summary>
    /// Meetup identity provider.
    /// </summary>
    Meetup,
    /// <summary>
    /// Luma identity provider.
    /// </summary>
    Luma,
    /// <summary>
    /// Eventbrite identity provider.
    /// </summary>
    Eventbrite
}
