using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using BunitCtx = Bunit.TestContext;

namespace Hackmum.Bethuya.Tests.UI;

/// <summary>
/// Render coverage for Community Passport pages that use strict Blazor Blueprint parameters.
/// </summary>
public sealed class CommunityPassportRenderTests
{
    private static readonly Guid SourceEventId = Guid.Parse("0199a9a4-1580-7dc0-99b3-09a0d9515af1");

    [Test]
    public async Task MemberPassport_RendersUnifiedExperienceAndPrivacyControls()
    {
        using var ctx = CreateContext(out var api);
        api.GetExperienceAsync(Arg.Any<CancellationToken>()).Returns(BuildExperience());

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityPassport>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-test='community-passport-header']");
            cut.Find("[data-test='passport-community-story']");
            cut.Find("[data-test='passport-signal']");
            cut.Find("[data-test='passport-activity-graph']");
            cut.Find("[data-test='passport-community-standing']");
            cut.Find("[data-test='passport-verified-communities']");
            cut.Find("[data-test='passport-primary-journal']");
            cut.Find("[data-test='passport-secondary-rail']");
            cut.Find("[data-test='passport-journey']");
            cut.Find("[data-test='passport-opportunities']");
            cut.Find("[data-test='passport-portfolio']");
            cut.Find("[data-test='passport-connections']");
            cut.Find("[data-test='passport-contributions']");
            cut.Find("[data-test='passport-privacy-section']");
            if (cut.FindAll("[data-test='community-passport-status']").Count > 0)
            {
                throw new InvalidOperationException("A status alert must render only when a real status message exists.");
            }
            if (cut.Markup.Contains(">Connect<", StringComparison.Ordinal)
                || cut.Markup.Contains("Followers", StringComparison.OrdinalIgnoreCase)
                || cut.Markup.Contains("Reputation score", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The Passport must not render social-network or reputation mechanics.");
            }
        });

        var eventLink = cut.Find("[data-test='passport-contribution-event-link']");
        if (eventLink.GetAttribute("href") != $"/events/{SourceEventId}")
        {
            throw new InvalidOperationException("Event-backed contributions must link to their source event.");
        }
        if (cut.FindAll("[data-test='passport-contribution-registrations-link']").Count > 0)
        {
            throw new InvalidOperationException("Member view must not expose organizer registration links.");
        }
    }

    [Test]
    public async Task PrivacyPanel_RendersStrictFormComponents()
    {
        using var ctx = CreateContext(out _);

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Components.Passport.PassportPrivacyPanel>(
            parameters => parameters.Add(
                panel => panel.Privacy,
                new PassportPrivacyPreferencesDto("CommunityOnly", true, true, true, true, true, true, true, true)));

        cut.Find("[data-test='passport-visibility-select']");
        cut.Find("[data-test='passport-save-privacy-btn']");
        cut.Find("[data-test='passport-mentorship-discovery-toggle']");
        await Task.CompletedTask;
    }

    [Test]
    public async Task MemberPassport_ShowsLoadingStateBeforeExperienceResolves()
    {
        using var ctx = CreateContext(out var api);
        var completion = new TaskCompletionSource<CommunityPassportExperienceDto>();
        api.GetExperienceAsync(Arg.Any<CancellationToken>()).Returns(completion.Task);

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityPassport>();

        cut.Find("[data-test='community-passport-loading']");
        completion.SetResult(BuildExperience());
    }

    [Test]
    public async Task OrganizerDirectory_RendersSearchFiltersAndMemberCards()
    {
        using var ctx = CreateContext(out var api);
        api.GetDirectoryAsync(null, null, 0, 24, Arg.Any<CancellationToken>())
            .Returns(new CommunityPassportDirectoryDto(
                1,
                [
                    new CommunityPassportDirectoryEntryDto(
                        Guid.NewGuid(),
                        "Test Member",
                        "Hackerspace Mumbai",
                        "Engineer",
                        ["Builder"],
                        DateTimeOffset.UtcNow.AddYears(-1),
                        true)
                ]));

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.CommunityPassportDirectory>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-test='passport-directory-search']");
            cut.Find("[data-test='passport-directory-sharing-filter']");
            cut.Find("[data-test='passport-directory-entry']");
        });
    }

    [Test]
    public async Task OrganizerPassport_RendersReadOnlyViewAndChampionAction()
    {
        using var ctx = CreateContext(out var api);
        var memberId = Guid.NewGuid();
        api.GetMemberExperienceAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(BuildExperience(isOrganizer: true, memberId));

        var cut = ctx.RenderComponent<global::Bethuya.Hybrid.Shared.Pages.OrganizerCommunityPassport>(
            parameters => parameters.Add(page => page.MemberId, memberId));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-test='organizer-community-passport']");
            cut.Find("[data-test='passport-award-champion-btn']");
            if (cut.Markup.Contains("passport-add-portfolio-btn", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Organizer view must not expose member portfolio editing.");
            }
        });

        var registrationsLink = cut.Find("[data-test='passport-contribution-registrations-link']");
        if (registrationsLink.GetAttribute("href") != $"/events/{SourceEventId}/registrations")
        {
            throw new InvalidOperationException("Organizer view must link to source registration records.");
        }
    }

    private static BunitCtx CreateContext(out ICommunityPassportApi api)
    {
        var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        api = Substitute.For<ICommunityPassportApi>();
        ctx.Services.AddSingleton(api);
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.AddTestAuthorization().SetAuthorized("Passport Tester");
        return ctx;
    }

    private static CommunityPassportExperienceDto BuildExperience(bool isOrganizer = false, Guid? memberId = null)
        => new(
            "1.0",
            new PassportViewerDto(!isOrganizer, isOrganizer, !isOrganizer, false),
            new PassportIdentitySummaryDto(
                memberId ?? Guid.NewGuid(),
                "Test Member",
                "test@example.com",
                "TM",
                "Hackerspace Mumbai",
                DateTimeOffset.UtcNow.AddYears(-1),
                "Engineer",
                "Bethuya"),
            new CommunityStoryDto(
                "Test Member has built a steady, evidence-backed community practice.",
                ["Attended a community event"]),
            [
                new CommunitySignalDto(
                    "Builder",
                    "Builder",
                    false,
                    "Visible because verified project activity exists.",
                    ["Contributed to Bethuya"])
            ],
            [
                new GrowthPathwayDto(
                    "Builder",
                    "Contributing",
                    [
                        new GrowthMilestoneDto("Explore", "Completed", "Joined"),
                        new GrowthMilestoneDto("Contribute", "Current", "Built with peers")
                    ])
            ],
            [
                new PassportContributionDto(
                    Guid.NewGuid(),
                    "ProjectContributed",
                    "Contributed to Bethuya",
                    "Implemented a community feature.",
                    "Hackerspace Mumbai",
                    "Platform",
                    DateTimeOffset.UtcNow.AddDays(-5),
                    true,
                    true,
                    SourceEventId)
            ],
            [new ActivityDayDto(DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), 1, ["Contributed to Bethuya"])],
            new PassportPortfolioDto(
                [new VerifiedPortfolioHighlightDto("Bethuya contribution", "Implemented a feature.", "Verified ledger entry")],
                []),
            [],
            [],
            new PassportPrivacyPreferencesDto("CommunityOnly", true, true, true, true, true, true, true, true),
            new PassportResidencyDto("South India", "India", "DPDP-ready"));
}
