using System.Text.RegularExpressions;
using Bethuya.Hybrid.Shared.Auth;
using Bethuya.Hybrid.Shared.Pages;
using Bethuya.Hybrid.Shared.Services;
using BlazorBlueprint.Components;
using Bunit.TestDoubles;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using BunitCtx = Bunit.BunitContext;

namespace Hackmum.Bethuya.Tests.UI;

public class MobileTouchLayoutTests
{
    private static readonly HashSet<string> HoverMediaQueries = new(StringComparer.Ordinal)
    {
        "@media (hover: hover) and (pointer: fine)",
        "@media (hover:hover) and (pointer:fine)",
    };

    [Test]
    public async Task Registrations_UsesOneColumnUntilLargeScreens()
    {
        await using var ctx = new BunitCtx();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization().SetAuthorized("Attendee").SetRoles(BethuyaRoles.Attendee);

        var eventId = Guid.Parse("019e263d-effb-7a55-86ec-170baeee3718");
        var registrationApi = Substitute.For<IRegistrationApi>();
        registrationApi.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<RegistrationDto>()));
        ctx.Services.AddSingleton(registrationApi);
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.Render<Registrations>(parameters => parameters.Add(p => p.EventId, eventId));

        var layout = cut.Find(".registrations-attendees").ParentElement!;
        await Assert.That(layout.ClassList.Contains("grid-cols-1")).IsTrue();
        await Assert.That(layout.ClassList.Contains("lg:grid-cols-3")).IsTrue();
        await Assert.That(layout.ClassList.Contains("grid-cols-3")).IsFalse();

        var css = File.ReadAllText(FindRepositoryFile(
            "src", "Bethuya.Hybrid", "Bethuya.Hybrid.Shared", "Pages", "Registrations.razor.css"));
        await Assert.That(css).Contains("grid-column: span 2 / span 2;");
    }

    [Test]
    public async Task CurationCss_StacksIntoPageFlowOnNarrowScreens()
    {
        var css = File.ReadAllText(FindRepositoryFile(
            "src", "Bethuya.Hybrid", "Bethuya.Hybrid.Web", "Components", "Pages", "CurationView.razor.css"));

        var narrow = css[css.IndexOf("@media (max-width: 980px)", StringComparison.Ordinal)..];
        await Assert.That(narrow).Contains("position: static;");
        await Assert.That(css).Contains("height: 100dvh;");
        await Assert.That(css).Contains("@media (prefers-reduced-motion: reduce)");
    }

    [Test]
    public async Task TransformHoverEffects_AreLimitedToFinePointers()
    {
        var sourceRoot = Path.GetDirectoryName(FindRepositoryFile("src", "Bethuya.Hybrid", "Bethuya.Hybrid.Shared", "wwwroot", "app.css"))!;
        var hybridRoot = Path.GetFullPath(Path.Combine(sourceRoot, "..", ".."));
        var ungated = Directory.EnumerateFiles(hybridRoot, "*.css", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains("bootstrap", StringComparison.Ordinal))
            .SelectMany(path => FindUngatedTransformHovers(File.ReadAllText(path)).Select(selector => $"{Path.GetFileName(path)}: {selector}"))
            .ToList();

        await Assert.That(ungated).IsEmpty();
    }

    [Test]
    public async Task SharedStyles_UseDarkNativeControlsAndAvoidIosFocusZoom()
    {
        var themeCss = File.ReadAllText(FindRepositoryFile(
            "src", "Bethuya.Hybrid", "Bethuya.Hybrid.Shared", "wwwroot", "bethuya-theme.css"));
        var appCss = File.ReadAllText(FindRepositoryFile(
            "src", "Bethuya.Hybrid", "Bethuya.Hybrid.Shared", "wwwroot", "app.css"));

        await Assert.That(themeCss).Contains("color-scheme: dark;");
        await Assert.That(appCss).Contains("@media (pointer: coarse)");
        await Assert.That(appCss).Contains("font-size: 16px;");
    }

    [Test]
    public async Task HoverScanner_OnlyAcceptsTheExactFinePointerQuery()
    {
        const string hoverRule = "{ .card:hover { transform: translateY(-1px); } }";

        await Assert.That(FindUngatedTransformHovers("@media (hover: hover) and (pointer: fine) " + hoverRule)).IsEmpty();
        await Assert.That(FindUngatedTransformHovers("@media (hover:hover) and (pointer:fine) " + hoverRule)).IsEmpty();
        await Assert.That(FindUngatedTransformHovers("@media (hover: hover) and (pointer: fine), (pointer: coarse) " + hoverRule))
            .Contains(".card:hover");
    }

    private static IEnumerable<string> FindUngatedTransformHovers(string css)
    {
        css = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var gatedDepths = new Stack<int>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] == '{')
            {
                var prelude = css[start..i].Trim();
                if (HoverMediaQueries.Contains(Regex.Replace(prelude, @"\s+", " ")))
                {
                    gatedDepths.Push(depth);
                }
                else if (prelude.Contains(":hover", StringComparison.Ordinal) && gatedDepths.Count == 0)
                {
                    var end = css.IndexOf('}', i);
                    if (css[i..end].Contains("transform", StringComparison.Ordinal))
                    {
                        yield return prelude;
                    }
                }

                depth++;
                start = i + 1;
            }
            else if (css[i] == '}')
            {
                depth--;
                if (gatedDepths.Count > 0 && gatedDepths.Peek() == depth)
                {
                    gatedDepths.Pop();
                }

                start = i + 1;
            }
            else if (css[i] == ';')
            {
                start = i + 1;
            }
        }
    }

    private static string FindRepositoryFile(params string[] pathSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. pathSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(pathSegments));
    }
}
