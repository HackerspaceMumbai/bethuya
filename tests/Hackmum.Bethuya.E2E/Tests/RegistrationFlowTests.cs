using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class RegistrationFlowTests : BethuyaE2ETest
{
    [Ignore("Registrations page requires authentication (RequireAttendee policy) — skipped until auth E2E setup")]
    [TestMethod]
    public async Task SubmitRegistration_ShouldShowInList()
    {
        await GotoWithBudgetAsync("/events/00000000-0000-0000-0000-000000000001/registrations");

        // Wait for Blazor Server circuit before interacting with form
        var submitBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Submit Registration" });
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

        await Page.GetByPlaceholder("Full name").FillAsync("Test Attendee");
        await Page.GetByPlaceholder("Email").FillAsync("test@example.com");
        await Page.GetByPlaceholder("Short bio").FillAsync("A passionate developer");
        await Page.GetByPlaceholder("Interests").FillAsync("AI, Blazor, .NET");

        await submitBtn.ClickAsync();

        var registrationRow = Page.Locator("[data-test='registration-row']").First;
        await Assertions.Expect(registrationRow).ToContainTextAsync("Test Attendee");
    }
}
