using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Bethuya.IntegrationTests;

/// <summary>
/// Regression coverage for a two-part contract bug between the Refit client
/// (<c>IImportApi.UploadAndRunDryRunAsync</c>) and the Minimal API endpoint
/// (<c>ImportEndpoints.UploadAndRunDryRunAsync</c>) that caused every dry-run upload from
/// the organizer UI to fail with a 400, with no visible feedback in the Blazor page.
/// </summary>
/// <remarks>
/// <para>
/// Two distinct bugs stacked to cause this:
/// </para>
/// <para>
/// 1. The backend handler originally declared <c>Guid eventId</c>/<c>Guid importTemplateId</c>/
/// <c>ImportKind importKind</c> without <c>[FromForm]</c>. Minimal API's parameter-source
/// inference does not treat sibling scalar parameters next to an <see cref="Microsoft.AspNetCore.Http.IFormFile"/>
/// as form fields by default — it looked for them in the query string and failed with
/// <c>BadHttpRequestException: Required parameter "Guid eventId" was not provided from query string.</c>
/// </para>
/// <para>
/// 2. After adding <c>[FromForm]</c>, requests from the real Refit client still failed. Refit's
/// <c>[Multipart]</c> attribute only special-cases <see cref="Refit.StreamPart"/>, byte arrays,
/// file parts, and plain <see cref="string"/> parameters as raw multipart values — any other
/// parameter type (like <see cref="Guid"/>) is routed through the configured JSON content
/// serializer, producing a literal quoted value such as <c>"01a0d800-..."</c> (embedded quote
/// characters) instead of a bare GUID string, which fails <c>Guid.TryParse</c>/model binding.
/// </para>
/// <para>
/// This test proves the fixed contract end-to-end against the real Aspire-orchestrated Backend:
/// posting genuine multipart form data with plain (unquoted) string values for
/// <c>eventId</c>/<c>importTemplateId</c>/<c>importKind</c> — exactly what the corrected
/// <c>IImportApi</c> now sends — succeeds with 200 OK and a populated dry-run result. A companion
/// assertion proves the JSON-quoted variant (the pre-fix Refit behavior) is rejected with 400,
/// so a future regression in either direction is caught.
/// </para>
/// </remarks>
[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ImportBatchMultipartBindingFlowTests(BethuyaAppFixture fixture)
{
    private const string PersonaHeaderName = "X-Bethuya-Dev-Persona";

    // The Luma Standard Registration Export system template seeded by ImportTemplateSeeder:
    // columns "Name", "Email", "Registered At" mapped to FullName/Email/OccurredAt.
    private const string LumaRegistrationTemplateName = "Luma Standard Registration Export";

    private const string SampleCsv =
        "Name,Email,Registered At\nAda Lovelace,ada@example.com,2024-01-01T10:00:00Z\n";

    [Test]
    public async Task UploadAndRunDryRun_PlainStringMultipartFields_Succeeds()
    {
        // Arrange — seed an event (as Vikram, who holds Admin so CanAccessEventAsync passes
        // regardless of CreatedBy) and resolve the seeded Luma Registration system template.
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var eventId = await CreateEventAsync(client);
        var templateId = await GetLumaRegistrationTemplateIdAsync(client);

        // Act — this is the exact wire format the corrected IImportApi produces: eventId and
        // importTemplateId sent as plain unquoted GUID strings, not JSON-serialized values.
        using var content = BuildMultipartContent(
            eventId.ToString(),
            templateId.ToString(),
            "Registration");

        var response = await client.PostAsync("/api/import/batches", content);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var batch = JsonSerializer.Deserialize<JsonElement>(body);
        await Assert.That(batch.GetProperty("eventId").GetGuid()).IsEqualTo(eventId);
        await Assert.That(batch.GetProperty("importTemplateId").GetGuid()).IsEqualTo(templateId);
        await Assert.That(batch.GetProperty("totalRows").GetInt32()).IsEqualTo(1);
        await Assert.That(batch.GetProperty("validRows").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task UploadAndRunDryRun_JsonQuotedGuidMultipartFields_Returns400()
    {
        // Arrange — same seeded event/template as above.
        using var client = fixture.CreateBackendClient();
        client.DefaultRequestHeaders.Add(PersonaHeaderName, "Vikram");

        var eventId = await CreateEventAsync(client);
        var templateId = await GetLumaRegistrationTemplateIdAsync(client);

        // Act — this reproduces the pre-fix Refit behavior: a Guid multipart part serialized
        // through System.Text.Json, embedding literal quote characters in the field value.
        using var content = BuildMultipartContent(
            $"\"{eventId}\"",
            templateId.ToString(),
            "Registration");

        var response = await client.PostAsync("/api/import/batches", content);

        // Assert — must fail fast with a client error, not silently succeed or 500.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static MultipartFormDataContent BuildMultipartContent(
        string eventId,
        string importTemplateId,
        string importKind)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(eventId), "eventId" },
            { new StringContent(importTemplateId), "importTemplateId" },
            { new StringContent(importKind), "importKind" }
        };

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(SampleCsv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "registrations.csv");

        return content;
    }

    private static async Task<Guid> CreateEventAsync(HttpClient client)
    {
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var response = await client.PostAsJsonAsync("/api/events", new
        {
            Title = $"Import Regression Test {Guid.NewGuid():N}",
            Description = (string?)null,
            Type = "Meetup",
            Capacity = 50,
            StartDate = start,
            EndDate = start.AddHours(2),
            Location = "Mumbai",
            CreatedBy = "dev-persona-vikram",
            Hashtag = (string?)null,
            CoverImageUrl = (string?)null
        });

        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> GetLumaRegistrationTemplateIdAsync(HttpClient client)
    {
        var templates = await client.GetFromJsonAsync<JsonElement>(
            "/api/import/templates?importKind=Registration");

        var luma = templates
            .EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == LumaRegistrationTemplateName);

        return luma.GetProperty("id").GetGuid();
    }
}
