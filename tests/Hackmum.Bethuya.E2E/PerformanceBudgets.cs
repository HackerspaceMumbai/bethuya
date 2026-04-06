namespace Hackmum.Bethuya.E2E;

/// <summary>
/// Performance timing budgets for E2E tests.
/// All Playwright tests must complete within these limits.
/// Derived from project targets in AGENTS.md.
/// Values can be overridden via environment variables to accommodate CI cold-start JIT costs
/// while keeping production budget targets tight.
/// </summary>
public static class PerformanceBudgets
{
    private static int Get(string envVar, int defaultMs) =>
        int.TryParse(Environment.GetEnvironmentVariable(envVar), out var v) ? v : defaultMs;

    /// <summary>Maximum time for initial page load (cold navigation). Override: BETHUYA_BUDGET_PAGE_LOAD_MS</summary>
    public static int PageLoadMs => Get("BETHUYA_BUDGET_PAGE_LOAD_MS", 5_000);

    /// <summary>Maximum time for SPA navigation (warm, client-side route change). Override: BETHUYA_BUDGET_NAVIGATION_MS</summary>
    public static int NavigationMs => Get("BETHUYA_BUDGET_NAVIGATION_MS", 2_000);

    /// <summary>Maximum time for form submission + server processing + redirect. Override: BETHUYA_BUDGET_FORM_SUBMIT_MS</summary>
    public static int FormSubmitMs => Get("BETHUYA_BUDGET_FORM_SUBMIT_MS", 5_000);

    /// <summary>Maximum time for API response (aligns with hot-path p99 target with E2E overhead). Override: BETHUYA_BUDGET_API_RESPONSE_MS</summary>
    public static int ApiResponseMs => Get("BETHUYA_BUDGET_API_RESPONSE_MS", 1_000);

    /// <summary>Maximum time for file upload (includes network transfer to Cloudinary). Override: BETHUYA_BUDGET_FILE_UPLOAD_MS</summary>
    public static int FileUploadMs => Get("BETHUYA_BUDGET_FILE_UPLOAD_MS", 10_000);

    /// <summary>Maximum time for Blazor interactive rendering to be ready after navigation. Override: BETHUYA_BUDGET_INTERACTIVE_READY_MS</summary>
    public static int InteractiveReadyMs => Get("BETHUYA_BUDGET_INTERACTIVE_READY_MS", 3_000);
}
