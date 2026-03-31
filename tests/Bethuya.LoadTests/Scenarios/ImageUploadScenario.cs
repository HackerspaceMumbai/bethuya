using System.Net.Http.Headers;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace Bethuya.LoadTests.Scenarios;

/// <summary>
/// NBomber scenario: Image upload endpoint under load.
/// Uses a minimal valid PNG (1x1 pixel) to test the full upload pipeline.
/// Target: p99 &lt; 180ms @ 200 RPS (lower than API endpoints due to Cloudinary round-trips).
/// </summary>
public static class ImageUploadScenario
{
    // Minimal valid 1x1 pixel PNG
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // 8-bit RGB
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, // compressed
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, // data
        0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    public static ScenarioProps Create(string baseUrl)
    {
        HttpClient? httpClient = null;

        return Scenario.Create("image_upload", async context =>
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(MinimalPng);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "file", $"loadtest-{context.InvocationNumber}.png");

            var request = Http.CreateRequest("POST", $"{baseUrl}/api/images/upload")
                .WithBody(content);

            return await Http.Send(httpClient!, request);
        })
        .WithInit(context =>
        {
            httpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
            return Task.CompletedTask;
        })
        .WithClean(context =>
        {
            httpClient?.Dispose();
            return Task.CompletedTask;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Image uploads are heavier — use lower RPS
            Simulation.RampingInject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5)),
            Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20)),
            Simulation.RampingInject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
        );
    }
}
