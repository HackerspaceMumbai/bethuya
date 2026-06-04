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

        // Section 0 — Contact
        await Page.GetByTestId("registration-name-field").GetByRole(AriaRole.Textbox).FillAsync("Test Attendee");
        await Page.GetByTestId("registration-email-field").GetByRole(AriaRole.Textbox).FillAsync("test@example.com");
        await Page.GetByTestId("registration-interests-field").GetByRole(AriaRole.Textbox).FillAsync("AI, Blazor, .NET");

        // Section 1 — Intent (required)
        await Page.GetByTestId("registration-why-attend-field").GetByRole(AriaRole.Textbox).FillAsync("I want to learn about AI and community building.");
        await Page.GetByTestId("contrib-learn").CheckAsync();
        await Page.GetByTestId("contrib-share").CheckAsync();

        // Section 2 — Experience
        await Page.GetByTestId("registration-experience-field").GetByLabel("Intermediate").CheckAsync();

        // Section 3 — Event Requirements (Gov ID file upload not exercised in auth-skipped test)

        await Page.GetByTestId("consent-checkbox").CheckAsync();
        await submitBtn.ClickAsync();

        var registrationRow = Page.Locator("[data-test='registration-row']").First;
        await Assertions.Expect(registrationRow).ToContainTextAsync("Test Attendee");
    }
}
