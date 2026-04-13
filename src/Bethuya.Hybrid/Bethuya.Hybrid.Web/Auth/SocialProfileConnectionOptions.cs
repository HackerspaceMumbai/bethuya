namespace Bethuya.Hybrid.Web.Auth;

/// <summary>Configuration for verified social profile connections used during onboarding.</summary>
public sealed class SocialProfileConnectionOptions
{
    public const string SectionName = "SocialConnections";

    public SocialOAuthOptions GitHub { get; set; } = new();

    public SocialOAuthOptions LinkedIn { get; set; } = new();
}

/// <summary>OAuth settings for a social provider.</summary>
public sealed class SocialOAuthOptions
{
    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public string CallbackPath { get; set; } = "";

    public string[] Scopes { get; set; } = [];
}
