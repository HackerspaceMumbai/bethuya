using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bethuya.IntegrationTests;

/// <summary>
/// Characterizes the Backend-observed identity/authorization outcome under the current
/// <c>Authentication:Provider=None</c> development authentication architecture, using the real
/// Aspire-orchestrated backend (Postgres-backed) rather than an in-memory TestServer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope limitation (documented per PR1 instructions):</b> this Aspire integration project
/// references only <c>AppHost</c> (see <c>Bethuya.IntegrationTests.csproj</c>), and
/// <see cref="BethuyaAppFixture"/> exposes an <see cref="HttpClient"/> only for the
/// <c>backend</c> resource (<see cref="BethuyaAppFixture.CreateBackendClient"/>). There is no
/// seam today to drive the Blazor <c>web</c> resource's rendered UI or its Refit-issued HTTP
/// calls from this test project — doing so would require either a browser-driving tool (e.g.
/// Playwright, out of scope for a backend integration project) or new test-only wiring in the
/// Web host, which would itself be "switching infrastructure" and is explicitly out of scope
/// for this characterization-only layer (PR1).
/// </para>
/// <para>
/// These tests therefore exercise the Backend directly, in isolation, which is exactly what
/// <c>docs/development-authentication.md</c> describes as already happening in practice: the
/// Backend authenticates every request as the fixed <c>dev-user-001</c> principal independently
/// of anything the Web tier does or believes, because Provider=None Web clients attach no
/// token/persona context to outbound Refit calls (see Program.cs <c>ConfigureBackendAuth</c>).
/// A full Web-&gt;Refit-&gt;Backend path is not representable in this project without the
/// persona-switching infrastructure this layer explicitly does not implement.
/// </para>
/// </remarks>
[ClassDataSource<BethuyaAppFixture>(Shared = SharedType.PerTestSession)]
public sealed class DevelopmentAuthenticationFlowTests(BethuyaAppFixture fixture)
{
    [Test]
    public async Task Backend_ProfileWrite_ResolvesSameIdentity_RegardlessOfClientSuppliedCredentials()
    {
        // A byte-identical-response assertion alone is NOT falsifiable here: GET
        // /api/profile/completion-status returns the exact same "no profile found" default
        // body for ANY userId that has no AttendeeProfile row, so two different (but both
        // profile-less) identities would produce identical bodies even if the Backend read
        // identity from the client-supplied bearer token. To make this genuinely test identity
        // *resolution* (not just response shape), one client WRITES a marked profile, and a
        // second client presenting different (and in one case entirely absent) credentials
        // reads it back. That is only possible if both requests resolved to the exact same
        // backend-authenticated principal, because POST /api/profile scopes the write to
        // GetUserId(user) and GET /api/profile scopes the read the same way.
        var marker = $"probe-{Guid.NewGuid():N}";

        using var clientWithBogusToken = fixture.CreateBackendClient();
        clientWithBogusToken.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "some-other-persona-claiming-attendee-only");

        var writeResponse = await clientWithBogusToken.PostAsJsonAsync("/api/profile", new
        {
            FirstName = marker,
            LastName = "IdentityProbe",
            Email = "identity-probe@bethuya.local",
            MobileNumber = (string?)null,
            GovernmentPhotoIdType = (string?)null,
            GovernmentIdLastFour = (string?)null,
            OccupationStatus = "Other",
            CompanyName = (string?)null,
            EducationInstitute = (string?)null,
            City = (string?)null,
            State = (string?)null,
            PostalCode = (string?)null,
            Country = (string?)null
        });
        await Assert.That(writeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var clientWithNoCredentials = fixture.CreateBackendClient();
        var readResponse = await clientWithNoCredentials.GetFromJsonAsync<JsonElement>("/api/profile");

        // If the Backend had honored the bogus-token client's claimed persona instead of its
        // own fixed dev principal, the write would have landed on a different userId row and
        // this credential-less read would see FirstName = null, not the marker.
        await Assert.That(readResponse.GetProperty("firstName").GetString()).IsEqualTo(marker);
    }

    [Test]
    public async Task Backend_CurationEndpoint_AuthorizesFixedPrincipalAgainstOrganizerOrCuratorPolicy()
    {
        // /api/curation/{eventId:guid} requires BethuyaPolicyNames.RequireOrganizerOrCurator.
        // A random, never-seeded eventId still passes authorization under the fixed dev
        // principal (which always holds Organizer and Curator roles) and reaches the business
        // logic, which then reports 404 for the missing event — not 401/403. This demonstrates
        // the Backend-observed *policy outcome* for the fixed dev principal without inventing a
        // new production endpoint.
        using var client = fixture.CreateBackendClient();
        var randomEventId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/curation/{randomEventId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
