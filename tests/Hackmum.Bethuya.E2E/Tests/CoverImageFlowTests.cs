using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hackmum.Bethuya.E2E.Tests;

/// <summary>
/// E2E tests for the event cover image upload flow.
/// All operations are budgeted per <see cref="PerformanceBudgets"/>.
/// </summary>
[TestClass]
public class CoverImageFlowTests : BethuyaE2ETest
{
    [TestMethod]
    public async Task PlanEvent_WithoutCoverImage_ShouldSucceed()
    {
        await GotoWithBudgetAsync("/events/plan");

        // Wait for Blazor Server circuit — submit button becomes enabled when interactive
        var submitBtn = Page.Locator("[data-test='save-draft-btn'] button");
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

        // Fill required fields only — no cover image
        await Page.GetByPlaceholder("Event title").FillAsync("No Cover Event");
        await Page.GetByPlaceholder("Event description").FillAsync("Event without a cover image");

        // Submit and verify redirect within budget
        await WithBudgetAsync("Form submit + redirect", PerformanceBudgets.FormSubmitMs, async () =>
        {
            await submitBtn.ClickAsync();
            await Page.WaitForURLAsync("**/events", new() { Timeout = PerformanceBudgets.FormSubmitMs });
        });

        // Verify events page loaded
        await Assertions.Expect(Page.Locator("[data-test='events-page']")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task PlanEvent_WithCoverImage_ShouldShowPreviewAndSucceed()
    {
        // This test requires Cloudinary credentials — skip when not configured
        var cloudinaryUrl = Environment.GetEnvironmentVariable("Cloudinary__CloudUrl")
            ?? Environment.GetEnvironmentVariable("CLOUDINARY_URL");
        if (string.IsNullOrEmpty(cloudinaryUrl))
        {
            Assert.Inconclusive("Skipping: Cloudinary credentials not configured (set CLOUDINARY_URL or Cloudinary__CloudUrl).");
            return;
        }

        await GotoWithBudgetAsync("/events/plan");

        // Wait for Blazor Server circuit
        var submitBtn = Page.Locator("[data-test='save-draft-btn'] button");
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new() { Timeout = PerformanceBudgets.InteractiveReadyMs });

        // Fill required fields
        await Page.GetByPlaceholder("Event title").FillAsync("Cover Image Event");
        await Page.GetByPlaceholder("Event description").FillAsync("Event with a cover image upload");

        // Locate the BbFileUpload file input — wrapper uses data-test='cover-image-dropzone'
        var fileInput = Page.Locator("[data-test='cover-image-dropzone'] input[type='file']");
        Assert.IsTrue(await fileInput.CountAsync() > 0,
            "Cover image file input ([data-test='cover-image-dropzone'] input[type='file']) not found on the page");

        // Create a minimal valid PNG for upload
        var pngPath = Path.Combine(Path.GetTempPath(), "e2e-test-cover.png");
        await File.WriteAllBytesAsync(pngPath, CreateMinimalPng());

        try
        {
            await WithBudgetAsync("File upload to Cloudinary", PerformanceBudgets.FileUploadMs, async () =>
            {
                await fileInput.SetInputFilesAsync(pngPath);

                // Upload is server-side (Refit → backend → Cloudinary); wait for the
                // preview to appear in the UI rather than watching browser network traffic.
                await Assertions.Expect(Page.Locator("[data-test='cover-image-preview']"))
                    .ToBeVisibleAsync(new() { Timeout = PerformanceBudgets.FileUploadMs });
            });
        }
        finally
        {
            File.Delete(pngPath);
        }

        // Submit and verify redirect within budget
        await WithBudgetAsync("Form submit + redirect", PerformanceBudgets.FormSubmitMs, async () =>
        {
            await submitBtn.ClickAsync();
            await Page.WaitForURLAsync("**/events", new() { Timeout = PerformanceBudgets.FormSubmitMs });
        });

        // Verify events page loaded
        await Assertions.Expect(Page.Locator("[data-test='events-page']")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task EventsPage_ShouldLoadWithinBudget()
    {
        // Ensure events page loads within page load budget even with cover images
        await GotoWithBudgetAsync("/events");

        await WithBudgetAsync("Events list interactive", PerformanceBudgets.InteractiveReadyMs, async () =>
        {
            await Assertions.Expect(Page.Locator("[data-test='events-page']")).ToBeVisibleAsync();
        });
    }

    /// <summary>Creates a minimal valid 1x1 pixel PNG.</summary>
    private static byte[] CreateMinimalPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // 8-bit RGB
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
        0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];
}
