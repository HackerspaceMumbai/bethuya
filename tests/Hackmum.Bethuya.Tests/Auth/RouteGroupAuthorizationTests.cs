using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.AI.CopilotSdk;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ServiceDefaults.Auth;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR2: route-group separation. Asserts metadata-level authorization for the new role-based
/// route groups (/api/public|attendee|curator|organizer|admin/*) and verifies the legacy flat
/// aliases still resolve and carry the same policy (public reads stay anonymous).
/// </summary>
public sealed class RouteGroupAuthorizationTests
{
    // --- New group: public reads are anonymous, no policy ---

    [Test]
    public async Task PublicEvents_List_AllowsAnonymous_AndHasNoPolicy()
    {
        var route = Describe("GET", "/api/public/events");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsTrue();
        await Assert.That(route.Policies).IsEmpty();
    }

    [Test]
    public async Task PublicEvents_GetById_AllowsAnonymous()
    {
        var route = Describe("GET", "/api/public/events/{id:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsTrue();
        await Assert.That(route.Policies).IsEmpty();
    }

    [Test]
    public async Task PublicEvents_GetBySlug_AllowsAnonymous()
    {
        var route = Describe("GET", "/api/public/events/slug/{hashtag}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsTrue();
    }

    [Test]
    public async Task PublicEvents_ReadFairnessTargets_AllowsAnonymous()
    {
        var route = Describe("GET", "/api/public/events/{id:guid}/fairness-targets");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsTrue();
        await Assert.That(route.Policies).IsEmpty();
    }

    // --- New group: organizer ---

    [Test]
    public async Task OrganizerEvents_Create_RequiresOrganizer()
    {
        var route = Describe("POST", "/api/organizer/events");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsFalse();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task OrganizerEvents_Update_RequiresOrganizer()
    {
        var route = Describe("PUT", "/api/organizer/events/{id:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task OrganizerEvents_Delete_RequiresOrganizer()
    {
        var route = Describe("DELETE", "/api/organizer/events/{id:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task OrganizerEvents_WriteFairnessTargets_RequiresOrganizer()
    {
        var route = Describe("PUT", "/api/organizer/events/{id:guid}/fairness-targets");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task OrganizerAgents_Planner_RequiresOrganizer()
    {
        var route = Describe("POST", "/api/organizer/agents/planner/{eventId:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task OrganizerPlanningCycles_Start_RequiresOrganizer()
    {
        var route = Describe("POST", "/api/organizer/planning-cycles/events/{eventId:guid}/start");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task OrganizerImages_DirectUploadSession_RequiresOrganizer()
    {
        var route = Describe("POST", "/api/organizer/images/direct-upload/session");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    // --- New group: attendee ---

    [Test]
    public async Task AttendeeRegistrations_Create_RequiresAttendee()
    {
        var route = Describe("POST", "/api/attendee/registrations");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    [Test]
    public async Task AttendeeProfile_Get_RequiresAttendee()
    {
        var route = Describe("GET", "/api/attendee/profile");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    [Test]
    public async Task AttendeeProfile_Save_RequiresAttendee()
    {
        var route = Describe("POST", "/api/attendee/profile");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    [Test]
    public async Task AttendeeRegistrations_GovernmentIdUpload_RequiresAttendee()
    {
        var route = Describe("POST", "/api/attendee/registrations/{id:guid}/government-id");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsFalse();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    [Test]
    public async Task AttendeeRegistrations_ReadByEvent_RequiresAttendee()
    {
        var route = Describe("GET", "/api/attendee/registrations/event/{eventId:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsFalse();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    // --- New group: curator ---

    [Test]
    public async Task CuratorDashboard_RequiresCurator()
    {
        var route = Describe("GET", "/api/curator/curation/{eventId:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireCurator);
    }

    [Test]
    public async Task CuratorDecision_RequiresCurator()
    {
        var route = Describe("POST", "/api/curator/curation/{eventId:guid}/registrants/{registrationId:guid}/decision");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireCurator);
    }

    // --- New group: admin ---

    [Test]
    public async Task AdminApprovals_Pending_RequiresAdmin()
    {
        var route = Describe("GET", "/api/admin/approvals/pending");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAdmin);
    }

    [Test]
    public async Task AdminApprovals_Approve_RequiresAdmin()
    {
        var route = Describe("POST", "/api/admin/approvals/{id:guid}/approve");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAdmin);
    }

    [Test]
    public async Task AdminDevSeed_RequiresAdmin()
    {
        var route = Describe("POST", "/api/admin/dev/curation/seed");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAdmin);
    }

    // --- Legacy aliases: still resolve, same policy (public reads anonymous) ---

    [Test]
    public async Task LegacyEvents_List_StillResolves_AndAllowsAnonymous()
    {
        var route = Describe("GET", "/api/events");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.AllowAnonymous).IsTrue();
        await Assert.That(route.Policies).IsEmpty();
    }

    [Test]
    public async Task LegacyEvents_Create_StillResolves_AndRequiresOrganizer()
    {
        var route = Describe("POST", "/api/events");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task LegacyRegistrations_Create_StillResolves_AndRequiresAttendee()
    {
        var route = Describe("POST", "/api/registrations");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    [Test]
    public async Task LegacyCuration_StillResolves_AndRequiresCurator()
    {
        var route = Describe("GET", "/api/curation/{eventId:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireCurator);
    }

    [Test]
    public async Task LegacyApprovals_StillResolves_AndRequiresAdmin()
    {
        var route = Describe("GET", "/api/approvals/pending");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAdmin);
    }

    [Test]
    public async Task LegacyAgents_StillResolves_AndRequiresOrganizer()
    {
        var route = Describe("POST", "/api/agents/planner/{eventId:guid}");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task LegacyImages_StillResolves_AndRequiresOrganizer()
    {
        var route = Describe("POST", "/api/images/direct-upload/session");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireOrganizer);
    }

    [Test]
    public async Task LegacyProfile_StillResolves_AndRequiresAttendee()
    {
        var route = Describe("GET", "/api/profile");

        await Assert.That(route.Exists).IsTrue();
        await Assert.That(route.Policies).Contains(BethuyaPolicyNames.RequireAttendee);
    }

    private static RouteDescription Describe(string method, string pattern)
    {
        using var app = BuildApp();
        var normalizedTarget = Normalize(pattern);

        var endpoint = Endpoints(app).FirstOrDefault(e =>
            string.Equals(Normalize(e.RoutePattern.RawText), normalizedTarget, StringComparison.OrdinalIgnoreCase)
            && (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) ?? false));

        if (endpoint is null)
        {
            return new RouteDescription(false, false, []);
        }

        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();

        var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        return new RouteDescription(true, allowAnonymous, policies);
    }

    private static IEnumerable<RouteEndpoint> Endpoints(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>();

    private static string Normalize(string? rawText) =>
        string.IsNullOrEmpty(rawText)
            ? string.Empty
            : "/" + rawText.Trim('/');

    private static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder();

        // Endpoint metadata inference (RequestDelegateFactory) only checks whether a handler
        // parameter type is a registered service; the instances are never resolved here. Stub
        // every handler dependency so inference treats them as services (not inferred bodies).
        StubServices(builder);

        var app = builder.Build();

        app.MapEventEndpoints();
        app.MapImageEndpoints();
        app.MapRegistrationEndpoints();
        app.MapAgentEndpoints();
        app.MapCurationEndpoints();
        app.MapApprovalEndpoints();
        app.MapProfileEndpoints();
        app.MapPlanningCycleEndpoints();
        app.MapDevelopmentEndpoints();

        return app;
    }

    private static void StubServices(WebApplicationBuilder builder)
    {
        // The registration/government-id endpoints now inject IAuthorizationService for resource-based
        // ownership checks (PR4). Register the authorization services so metadata inference recognizes
        // it as a service rather than an inferred request body (which throws on GET/DELETE routes).
        builder.Services.AddAuthorization();

        Type[] handlerDependencies =
        [
            typeof(IEventRepository),
            typeof(IRegistrationRepository),
            typeof(IAttendeeProfileRepository),
            typeof(IDecisionRepository),
            typeof(IImageUploadService),
            typeof(IDateRecommendationService),
            typeof(BethuyaDbContext),
            typeof(InclusionSignalsNormalizer),
            typeof(CurationFairnessService),
            typeof(PlanningCycleService),
            typeof(CurationSampleSeeder),
            typeof(ApprovalWorkflow),
            typeof(IDataProtectionProvider),
            typeof(IAgent<PlannerRequest, PlannerResponse>),
            typeof(IAgent<CuratorRequest, CuratorResponse>),
            typeof(IAgent<FacilitatorRequest, FacilitatorResponse>),
            typeof(IAgent<ReporterRequest, ReporterResponse>),
            typeof(IUserContext),
            typeof(IAuthorizationAuditor)
        ];

        foreach (var dependency in handlerDependencies)
        {
            builder.Services.AddSingleton(
                dependency,
                static _ => throw new NotSupportedException("Stub service is not resolvable; metadata inspection only."));
        }
    }

    private sealed record RouteDescription(bool Exists, bool AllowAnonymous, IReadOnlyList<string> Policies);
}
