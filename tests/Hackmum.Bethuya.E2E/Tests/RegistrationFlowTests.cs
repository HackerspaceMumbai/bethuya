using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

[TestClass]
public class RegistrationFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task SubmitRegistration_ShouldShowInList()
    {
        await Page.GotoAsync("/events/00000000-0000-0000-0000-000000000001/registrations");

        await Page.GetByPlaceholder("Full name").FillAsync("Test Attendee");
        await Page.GetByPlaceholder("Email").FillAsync("test@example.com");
        await Page.GetByPlaceholder("Short bio").FillAsync("A passionate developer");
        await Page.GetByPlaceholder("Interests").FillAsync("AI, Blazor, .NET");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Registration" }).ClickAsync();

        var registrationRow = Page.Locator("[data-test='registration-row']").First;
        await Expect(registrationRow).ToContainTextAsync("Test Attendee");
    }
}
