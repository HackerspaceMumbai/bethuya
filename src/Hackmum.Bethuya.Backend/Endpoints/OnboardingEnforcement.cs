using Microsoft.Extensions.Configuration;

namespace Hackmum.Bethuya.Backend.Endpoints;

/// <summary>
/// Server-side onboarding enforcement helpers (PR3, finding M1). The UI onboarding gate
/// (<c>Onboarding:BypassMandatoryProfile</c>) configures the same switch the backend honors, so the
/// API independently rejects registration attempts when a mandatory attendee profile is incomplete.
/// </summary>
internal static class OnboardingEnforcement
{
    /// <summary>Configuration key that, when <c>true</c>, bypasses mandatory-profile enforcement.</summary>
    internal const string BypassMandatoryProfileKey = "Onboarding:BypassMandatoryProfile";

    /// <summary>Returns whether mandatory-profile enforcement is bypassed via configuration.</summary>
    internal static bool IsBypassEnabled(IConfiguration configuration) =>
        configuration.GetValue(BypassMandatoryProfileKey, defaultValue: false);
}
