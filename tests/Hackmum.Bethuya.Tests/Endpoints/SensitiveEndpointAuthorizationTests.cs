using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hackmum.Bethuya.AI.CopilotSdk;
using Hackmum.Bethuya.Agents.Base;
using Hackmum.Bethuya.Agents.Contracts;
using Hackmum.Bethuya.Agents.Workflows;
using Hackmum.Bethuya.Backend.Agents;
using Hackmum.Bethuya.Backend.Contracts;
using Hackmum.Bethuya.Backend.Endpoints;
using Hackmum.Bethuya.Backend.Services;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Repositories;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Tests.Workflows;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServiceDefaults.Auth;

namespace Hackmum.Bethuya.Tests.Endpoints;

/// <summary>
/// Registration, planning cycle, approval, agent and image endpoints must declare their own
/// policies because the authenticated fallback policy is off by default.
/// </summary>
public sealed class SensitiveEndpointAuthorizationTests
{
    private static readonly Guid EventId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548c01");
    private static readonly Guid RegistrationId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548c02");
    private static readonly Guid DecisionId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548c03");
    private static readonly Guid CycleId = Guid.Parse("01985f65-71aa-7e2b-9581-b3e8b0548c04");

    private enum Access
    {
        OrganizerOrCurator,
        Organizer
    }

    private static readonly (string Method, string Path, object? Body, Access Access)[] ProtectedRoutes =
    [
        ("GET", $"/api/registrations/event/{EventId}", null, Access.OrganizerOrCurator),
        ("GET", $"/api/registrations/{RegistrationId}", null, Access.OrganizerOrCurator),
        ("DELETE", $"/api/registrations/{RegistrationId}", null, Access.Organizer),
        ("POST", $"/api/planning-cycles/events/{EventId}/start", new StartPlanningCycleRequest(), Access.Organizer),
        ("POST", $"/api/planning-cycles/{CycleId}/draft", new GeneratePlannerDraftRequest("work-1"), Access.Organizer),
        ("POST", $"/api/planning-cycles/{CycleId}/approve", new ApprovePlannerDraftRequest(Guid.NewGuid(), "# Agenda", "spoofed"), Access.Organizer),
        ("POST", $"/api/planning-cycles/{CycleId}/publish", new PublishPlanningCycleRequest(Guid.NewGuid(), "spoofed"), Access.Organizer),
        ("GET", "/api/approvals/pending", null, Access.OrganizerOrCurator),
        ("GET", $"/api/approvals/registration/{RegistrationId}", null, Access.OrganizerOrCurator),
        ("POST", $"/api/approvals/{DecisionId}/approve", new ApproveRequest("ok"), Access.OrganizerOrCurator),
        ("POST", $"/api/approvals/{DecisionId}/reject", new RejectRequest("no"), Access.OrganizerOrCurator),
        ("POST", $"/api/agents/planner/{EventId}", new InvokePlannerRequest(), Access.Organizer),
        ("POST", $"/api/agents/curator/{EventId}", new InvokeCuratorRequest(new Dictionary<string, double>(), []), Access.OrganizerOrCurator),
        ("POST", $"/api/agents/facilitator/{EventId}", new InvokeFacilitatorRequest(), Access.Organizer),
        ("POST", $"/api/agents/reporter/{EventId}", new InvokeReporterRequest(), Access.Organizer),
        ("POST", "/api/agents/recommend-dates", new RecommendDatesApiRequest("Meetup"), Access.Organizer),
        ("POST", "/api/images/direct-upload/session", new ImageEndpoints.CreateDirectUploadSessionRequest("cover.png", "image/png", 2048), Access.Organizer),
        ("POST", "/api/images/direct-upload/delete", new ImageEndpoints.DeletePendingDirectUploadRequest("public-id", "token"), Access.Organizer),
    ];

    [Test]
    public async Task ProtectedRoutes_DenyAnonymousAndAttendeeWithoutSideEffects()
    {
        await using var fixture = await TestFixture.CreateAsync();
        using var client = fixture.App.GetTestClient();

        foreach (var (method, path, body, _) in ProtectedRoutes)
        {
            using var anonymous = await SendAsync(client, method, path, body, role: null);
            await Assert.That(anonymous.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized).Because($"anonymous {method} {path}");

            using var attendee = await SendAsync(client, method, path, body, BethuyaRoleNames.Attendee);
            await Assert.That(attendee.StatusCode).IsEqualTo(HttpStatusCode.Forbidden).Because($"attendee {method} {path}");
        }

        await Assert.That(fixture.ReceivedCallCount()).IsEqualTo(0);
    }

    [Test]
    public async Task ProtectedRoutes_AllowOnlyTheirRoles()
    {
        await using var fixture = await TestFixture.CreateAsync();
        using var client = fixture.App.GetTestClient();

        foreach (var (method, path, body, access) in ProtectedRoutes)
        {
            foreach (var role in new[] { BethuyaRoleNames.Curator, BethuyaRoleNames.Organizer, BethuyaRoleNames.Admin })
            {
                using var response = await SendAsync(client, method, path, body, role);
                var allowed = role != BethuyaRoleNames.Curator || access == Access.OrganizerOrCurator;

                if (allowed)
                {
                    await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized).Because($"{role} {method} {path}");
                    await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden).Because($"{role} {method} {path}");
                }
                else
                {
                    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden).Because($"{role} {method} {path}");
                }
            }
        }
    }

    [Test]
    public async Task ActivePlanningCycleRead_RequiresSignedInUser()
    {
        await using var fixture = await TestFixture.CreateAsync();
        using var client = fixture.App.GetTestClient();
        var path = $"/api/planning-cycles/events/{EventId}/active";

        using var anonymous = await SendAsync(client, "GET", path, null, role: null);
        using var attendee = await SendAsync(client, "GET", path, null, BethuyaRoleNames.Attendee);

        await Assert.That(anonymous.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(attendee.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ApprovalDecision_RecordsAuthenticatedReviewer()
    {
        await using var fixture = await TestFixture.CreateAsync();
        using var client = fixture.App.GetTestClient();

        using var response = await SendAsync(
            client, "POST", $"/api/approvals/{DecisionId}/approve", new ApproveRequest("ok"), BethuyaRoleNames.Curator);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await fixture.DecisionRepository.Received(1).UpdateAsync(
            Arg.Is<Decision>(d => d.DecidedBy == "reviewer@example.com" && d.Status == DecisionStatus.Applied),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PlanningCycleApproveAndPublish_UseAuthenticatedSubjectInsteadOfClientValue()
    {
        await using var fixture = await TestFixture.CreateAsync();
        using var client = fixture.App.GetTestClient();
        await fixture.SeedEventAsync();

        using var start = await SendAsync(
            client, "POST", $"/api/planning-cycles/events/{EventId}/start", new StartPlanningCycleRequest(), BethuyaRoleNames.Organizer);
        var cycle = await start.Content.ReadFromJsonAsync<PlanningCycleResponse>();
        using var draft = await SendAsync(
            client, "POST", $"/api/planning-cycles/{cycle!.CycleId}/draft", new GeneratePlannerDraftRequest("work-1"), BethuyaRoleNames.Organizer);
        cycle = await draft.Content.ReadFromJsonAsync<PlanningCycleResponse>();
        var activeDraft = cycle!.ActiveDraft!;

        using var approve = await SendAsync(
            client,
            "POST",
            $"/api/planning-cycles/{cycle.CycleId}/approve",
            new ApprovePlannerDraftRequest(activeDraft.DraftId, activeDraft.MarkdownAgenda, "spoofed-approver"),
            BethuyaRoleNames.Organizer);
        using var publish = await SendAsync(
            client,
            "POST",
            $"/api/planning-cycles/{cycle.CycleId}/publish",
            new PublishPlanningCycleRequest(activeDraft.DraftId, "spoofed-publisher"),
            BethuyaRoleNames.Organizer);

        await Assert.That(approve.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(publish.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var published = await publish.Content.ReadFromJsonAsync<PublishedScheduleResponse>();
        await Assert.That(published!.PublishedBy).IsEqualTo("authenticated-user");

        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
        var storedDraft = await db.PlannerDrafts.SingleAsync(d => d.Id == activeDraft.DraftId);
        await Assert.That(storedDraft.ApprovalDecision).IsEqualTo("Approved by authenticated-user");
    }

    [Test]
    public async Task GovernmentIdUpload_AllowsOwnerAndOrganizerOnly()
    {
        await using var fixture = await TestFixture.CreateAsync();
        using var client = fixture.App.GetTestClient();
        var registration = await fixture.SeedOwnedRegistrationAsync(ownerUserId: "registration-owner");
        var path = $"/api/registrations/{registration.Id}/government-id";

        using var anonymous = await SendGovernmentIdAsync(client, path, role: null);
        using var otherAttendee = await SendGovernmentIdAsync(client, path, BethuyaRoleNames.Attendee);
        using var curator = await SendGovernmentIdAsync(client, path, BethuyaRoleNames.Curator);

        await Assert.That(anonymous.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(otherAttendee.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(curator.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await fixture.RegistrationRepository.DidNotReceive().UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>());

        using var owner = await SendGovernmentIdAsync(client, path, BethuyaRoleNames.Attendee, subject: "registration-owner");
        using var organizer = await SendGovernmentIdAsync(client, path, BethuyaRoleNames.Organizer);

        await Assert.That(owner.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(organizer.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await fixture.RegistrationRepository.Received(2).UpdateAsync(registration, Arg.Any<CancellationToken>());
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, object? body, string? role)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (role is not null)
        {
            request.Headers.Add("X-Test-Role", role);
        }

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendGovernmentIdAsync(HttpClient client, string path, string? role, string? subject = null)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new MultipartFormDataContent { { file, "file", "id.png" } }
        };

        if (role is not null)
        {
            request.Headers.Add("X-Test-Role", role);
        }

        if (subject is not null)
        {
            request.Headers.Add("X-Test-Subject", subject);
        }

        return client.SendAsync(request);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly object[] _substitutes;

        private TestFixture(WebApplication app, IRegistrationRepository registrationRepository, IDecisionRepository decisionRepository, object[] substitutes)
        {
            App = app;
            RegistrationRepository = registrationRepository;
            DecisionRepository = decisionRepository;
            _substitutes = substitutes;
        }

        public WebApplication App { get; }

        public IRegistrationRepository RegistrationRepository { get; }

        public IDecisionRepository DecisionRepository { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var registrationRepository = Substitute.For<IRegistrationRepository>();
            var decisionRepository = Substitute.For<IDecisionRepository>();
            var eventRepository = Substitute.For<IEventRepository>();
            var planner = Substitute.For<IAgent<PlannerRequest, PlannerResponse>>();
            var curator = Substitute.For<IAgent<CuratorRequest, CuratorResponse>>();
            var facilitator = Substitute.For<IAgent<FacilitatorRequest, FacilitatorResponse>>();
            var reporter = Substitute.For<IAgent<ReporterRequest, ReporterResponse>>();
            var dateService = Substitute.For<IDateRecommendationService>();
            var imageService = Substitute.For<IImageUploadService>();

            registrationRepository.GetByEventIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Registration>());
            registrationRepository.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
                .Returns(new Registration { Id = RegistrationId, EventId = EventId, FullName = "Registrant", Email = "registrant@example.com" });
            decisionRepository.GetByIdAsync(DecisionId, Arg.Any<CancellationToken>())
                .Returns(_ => new Decision { Id = DecisionId, EntityType = "registration", EntityId = RegistrationId, DecidedBy = "requester@example.com" });
            eventRepository.GetByIdAsync(EventId, Arg.Any<CancellationToken>())
                .Returns(new Event { Id = EventId, Title = "Authorization Test Event", CreatedBy = "organizer" });
            dateService.RecommendAsync(Arg.Any<DateRecommendationContext>(), Arg.Any<CancellationToken>())
                .Returns(new DateRecommendation(new DateOnly(2026, 12, 5), new TimeOnly(10, 0), new DateOnly(2026, 12, 5), new TimeOnly(13, 0)));
            imageService.CreateDirectUploadSessionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(new DirectImageUploadSession(
                    "https://api.cloudinary.com/v1_1/demo/image/upload", "demo", "key", "public-id", 1, "signature", "token", null, 1024, "png"));

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication("test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
            builder.AddBethuyaAuthorization();
            builder.Services.AddDataProtection();
            var databaseName = $"sensitive-endpoint-auth-{Guid.NewGuid():N}";
            builder.Services.AddDbContext<BethuyaDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            builder.Services.AddSingleton(registrationRepository);
            builder.Services.AddSingleton(decisionRepository);
            builder.Services.AddSingleton(eventRepository);
            builder.Services.AddSingleton(Substitute.For<IAttendeeProfileRepository>());
            builder.Services.AddSingleton(planner);
            builder.Services.AddSingleton(curator);
            builder.Services.AddSingleton(facilitator);
            builder.Services.AddSingleton(reporter);
            builder.Services.AddSingleton(dateService);
            builder.Services.AddSingleton(imageService);
            builder.Services.AddSingleton(new ApprovalWorkflow(NullLogger<ApprovalWorkflow>.Instance));
            builder.Services.AddSingleton<IAgentInvoker>(new PlanningCycleWorkflowTests.FakeAgentInvoker());
            builder.Services.AddScoped<PlanningCycleService>();
            builder.Services.AddScoped<InclusionSignalsNormalizer>();
            builder.Services.AddScoped<CommunityPassportService>();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapRegistrationEndpoints();
            app.MapPlanningCycleEndpoints();
            app.MapApprovalEndpoints();
            app.MapAgentEndpoints();
            app.MapImageEndpoints();
            await app.StartAsync();

            return new TestFixture(
                app,
                registrationRepository,
                decisionRepository,
                [registrationRepository, decisionRepository, eventRepository, planner, curator, facilitator, reporter, dateService, imageService]);
        }

        public int ReceivedCallCount() => _substitutes.Sum(substitute => substitute.ReceivedCalls().Count());

        public async Task SeedEventAsync()
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
            db.Events.Add(new Event
            {
                Id = EventId,
                Title = "Planner Workflow Test Event",
                Type = EventType.Meetup,
                Capacity = 120,
                StartDate = new DateTimeOffset(2026, 6, 30, 9, 30, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2026, 6, 30, 13, 0, 0, TimeSpan.Zero),
                Location = "Mumbai",
                CreatedBy = "organizer"
            });
            await db.SaveChangesAsync();
        }

        public async Task<Registration> SeedOwnedRegistrationAsync(string ownerUserId)
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BethuyaDbContext>();
            var member = new CommunityMember
            {
                UserId = ownerUserId,
                DisplayName = "Owner",
                Email = "owner@example.com"
            };
            db.CommunityMembers.Add(member);
            await db.SaveChangesAsync();

            var registration = new Registration
            {
                EventId = EventId,
                CommunityMemberId = member.Id,
                FullName = "Owner",
                Email = "owner@example.com"
            };
            RegistrationRepository.GetByIdAsync(registration.Id, Arg.Any<CancellationToken>()).Returns(registration);
            return registration;
        }

        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new("sub", Request.Headers["X-Test-Subject"].FirstOrDefault() ?? "authenticated-user"),
                new(ClaimTypes.Email, "reviewer@example.com"),
                new(ClaimTypes.Role, role)
            ];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
