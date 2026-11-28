using System.Text.RegularExpressions;

namespace Hackmum.Bethuya.Tests.UI;

public partial class CssCorrectnessTests
{
    [Test]
    public async Task Stylesheets_DoNotWrapHexColorTokensInHsl()
    {
        var violations = FindViolations(css => HslVarPattern().IsMatch(css));

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Stylesheets_DoNotNestKeyframesInsideKeyframes()
    {
        var violations = FindViolations(HasNestedKeyframes);

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Stylesheets_DoNotUseUndefinedForegroundTokensWithoutFallback()
    {
        var violations = FindViolations(css => UndefinedForegroundTokenPattern().IsMatch(css));

        await Assert.That(violations).IsEmpty();
    }

    private static List<string> FindViolations(Func<string, bool> isViolation)
    {
        var hybridRoot = FindRepositoryDirectory("src", "Bethuya.Hybrid");
        var separator = Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(hybridRoot, "*.css", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                && !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                && !path.Contains($"{separator}bootstrap{separator}", StringComparison.Ordinal))
            .Where(path => isViolation(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(hybridRoot, path))
            .ToList();
    }

    private static bool HasNestedKeyframes(string css)
    {
        var depth = 0;
        var keyframesDepth = -1;

        for (var i = 0; i < css.Length; i++)
        {
            if (css.AsSpan(i).StartsWith("@keyframes", StringComparison.Ordinal))
            {
                if (keyframesDepth >= 0)
                {
                    return true;
                }

                keyframesDepth = depth;
            }
            else if (css[i] == '{')
            {
                depth++;
            }
            else if (css[i] == '}')
            {
                depth--;
                if (depth == keyframesDepth)
                {
                    keyframesDepth = -1;
                }
            }
        }

        return false;
    }

    private static string FindRepositoryDirectory(params string[] pathSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. pathSegments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find repository directory: {Path.Combine(pathSegments)}");
    }

    [GeneratedRegex(@"hsl\(\s*var\(")]
    private static partial Regex HslVarPattern();

    [GeneratedRegex(@"var\(\s*--hs-(muted-)?foreground\s*\)")]
    private static partial Regex UndefinedForegroundTokenPattern();
}
