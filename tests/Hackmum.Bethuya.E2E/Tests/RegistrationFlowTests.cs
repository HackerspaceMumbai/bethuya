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

        await Page.GetByPlaceholder("Tell us what draws you to this event and why it matters to you.").FillAsync("I want to meet builders and contribute to the event.");
        await Page.GetByLabel("Present/demo").CheckAsync();
        await Page.GetByLabel("Advanced").CheckAsync();
        await Page.GetByLabel("Definitely").CheckAsync();
        await Page.GetByLabel("Within city").CheckAsync();

        await submitBtn.ClickAsync();

        var registrationRow = Page.Locator("[data-test='registration-row']").First;
        await Assertions.Expect(registrationRow).ToContainTextAsync("Test Attendee");
    }
}
