namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed class GitHubEventRepositoryOptions
{
    public const string SectionName = "GitHubEvents";

    public string Owner { get; set; } = "HackerspaceMumbai";

    /// <summary>
    /// Gets or sets the GitHub archive repository that receives event snapshots.
    /// This defaults to the public <c>events</c> repository.
    /// </summary>
    public string Repository { get; set; } = "events";

    public string Branch { get; set; } = "main";

    public string? Token { get; set; }
}
