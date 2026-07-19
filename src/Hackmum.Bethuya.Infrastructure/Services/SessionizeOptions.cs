namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed class SessionizeOptions
{
    public const string SectionName = "Sessionize";

    public string BaseUrl { get; set; } = "https://sessionize.com";

    public string SessionPathTemplate { get; set; } = "/api/v2/{eventId}/view/Sessions";

    public string SpeakerPathTemplate { get; set; } = "/api/v2/{eventId}/view/Speakers";

    public string? ApiToken { get; set; }
}
