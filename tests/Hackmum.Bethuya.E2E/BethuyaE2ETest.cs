using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

namespace Hackmum.Bethuya.E2E;

/// <summary>
/// Base class for Bethuya E2E tests with common configuration.
/// Uses Playwright MSTest integration for browser lifecycle management.
/// </summary>
public class BethuyaE2ETest : PageTest
{
    protected static string BaseUrl => "https://localhost:5001";

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl
        };
    }
}
