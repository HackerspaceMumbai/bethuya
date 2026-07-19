namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed class GitHubEventRepositoryOptions
{
    public const string SectionName = "GitHubEvents";

    public string Owner { get; set; } = "HackerspaceMumbai";

    public string Repository { get; set; } = "bethuya";

    public string Branch { get; set; } = "main";

    public string? Token { get; set; }
}
