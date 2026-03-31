namespace Hackmum.Bethuya.E2E;

/// <summary>
/// Performance timing budgets for E2E tests.
/// All Playwright tests must complete within these limits.
/// Derived from project targets in AGENTS.md.
/// </summary>
public static class PerformanceBudgets
{
    /// <summary>Maximum time for initial page load (cold navigation).</summary>
    public const int PageLoadMs = 5000;

    /// <summary>Maximum time for SPA navigation (warm, client-side route change).</summary>
    public const int NavigationMs = 2000;

    /// <summary>Maximum time for form submission + server processing + redirect.</summary>
    public const int FormSubmitMs = 5000;

    /// <summary>Maximum time for API response (aligns with hot-path p99 target with E2E overhead).</summary>
    public const int ApiResponseMs = 1000;

    /// <summary>Maximum time for file upload (includes network transfer to Cloudinary).</summary>
    public const int FileUploadMs = 10000;

    /// <summary>Maximum time for Blazor interactive rendering to be ready after navigation.</summary>
    public const int InteractiveReadyMs = 3000;
}
