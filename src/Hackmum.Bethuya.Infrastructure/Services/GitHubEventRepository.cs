using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed class GitHubEventRepository(HttpClient httpClient, IOptions<GitHubEventRepositoryOptions> options)
    : IGitHubEventRepository
{
    public async Task<EventArchiveEntry?> GetArchiveEntryAsync(string folderPath, CancellationToken ct = default)
    {
        var settings = ConfigureClient();
        var targetRef = await GetRefAsync(settings, settings.Branch, ct)
            ?? throw new InvalidOperationException($"Configured archive branch '{settings.Branch}' does not exist.");
        return await GetArchiveEntryAtRefAsync(settings, folderPath, targetRef.Object.Sha, ct);
    }

    public async Task<EventArchivePreview> PreviewEventAsync(EventPublicationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = ConfigureClient();
        var targetRef = await GetRefAsync(settings, settings.Branch, ct)
            ?? throw new InvalidOperationException($"Configured archive branch '{settings.Branch}' does not exist.");
        var entry = await GetArchiveEntryAtRefAsync(settings, request.FolderPath, targetRef.Object.Sha, ct);
        return CreatePreview(request, entry);
    }

    public Task<EventArchivePreview> PreviewEventWithEntryAsync(
        EventPublicationRequest request,
        EventArchiveEntry entry,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(CreatePreview(request, entry));
    }

    private static EventArchivePreview CreatePreview(EventPublicationRequest request, EventArchiveEntry? entry)
    {
        var candidateReadme = EventArchiveContentMerger.MergeReadme(entry?.ReadmeMarkdown, request.ReadmeMarkdown);
        var candidateMetadata = EventArchiveContentMerger.MergeMetadata(entry?.MetadataYaml, request.MetadataJson);
        var tokenInput = string.Join(
            "\n",
            request.FolderPath,
            entry?.ReadmeSha ?? string.Empty,
            entry?.MetadataSha ?? string.Empty,
            candidateReadme,
            candidateMetadata);
        var previewToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenInput)));

        return new(
            request.FolderPath,
            previewToken,
            entry?.ReadmeMarkdown,
            candidateReadme,
            entry?.MetadataYaml,
            candidateMetadata,
            !string.Equals(entry?.ReadmeMarkdown, candidateReadme, StringComparison.Ordinal)
                || !string.Equals(entry?.MetadataYaml, candidateMetadata, StringComparison.Ordinal));
    }

    public async Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ConfirmedPreviewToken))
        {
            throw new ArchivePreviewMismatchException(
                "This archive request has no confirmed snapshot. Re-preview the archive and confirm again before publication.");
        }

        var settings = ConfigureClient();
        var targetRef = await GetRefAsync(settings, settings.Branch, ct)
            ?? throw new InvalidOperationException($"Configured archive branch '{settings.Branch}' does not exist.");
        var entryAtTarget = await GetArchiveEntryAtRefAsync(settings, request.FolderPath, targetRef.Object.Sha, ct);
        EventArchivePreview preview;
        try
        {
            preview = CreatePreview(request, entryAtTarget);
        }
        catch (InvalidOperationException exception)
        {
            throw new ArchivePreviewMismatchException(
                $"The archive can no longer be merged safely: {exception.Message} Resolve the archive structure, then re-preview and confirm again.",
                exception);
        }

        if (!string.Equals(preview.PreviewToken, request.ConfirmedPreviewToken, StringComparison.Ordinal))
        {
            throw new ArchivePreviewMismatchException(
                "The archive changed after organizer confirmation. Re-preview the archive and confirm again before publication.");
        }

        if (!preview.HasChanges)
        {
            throw new InvalidOperationException("The archive already matches this projection; there are no changes to publish.");
        }

        var branchName = $"bethuya/event-archive-{request.EventId.Value:N}";
        var existingBranch = await GetRefAsync(settings, branchName, ct);
        var pullRequest = await FindOpenPullRequestAsync(settings, branchName, existingBranch is not null, ct);
        EnsureDraftPullRequest(pullRequest);
        var parentSha = existingBranch?.Object.Sha ?? targetRef.Object.Sha;
        var parentCommit = await GetCommitAsync(settings, parentSha, ct);
        var branchAlreadyMatches = false;
        if (existingBranch is not null)
        {
            // Bind the entire tree, not just archive files: copied messages cannot hide unrelated edits.
            if (!parentCommit.Message.EndsWith(
                    $"\nBethuya-Archive-Tree: {parentCommit.Tree.Sha}",
                    StringComparison.Ordinal))
            {
                throw new ArchivePreviewMismatchException(
                    "The archive PR branch contains human changes or lacks recorded Bethuya tree provenance. "
                    + "Preserve those changes through human review; after merging the PR, have an organizer retire the branch, "
                    + "then re-preview and confirm again. The branch was not changed.");
            }

            var branchEntry = await GetArchiveEntryAtRefAsync(settings, request.FolderPath, existingBranch.Object.Sha, ct);
            branchAlreadyMatches = branchEntry is not null
                && string.Equals(branchEntry.ReadmeMarkdown, preview.CandidateReadme, StringComparison.Ordinal)
                && string.Equals(branchEntry.MetadataYaml, preview.CandidateMetadata, StringComparison.Ordinal);
        }

        if (!branchAlreadyMatches)
        {
            var tree = await CreateTreeAsync(
                settings,
                parentCommit.Tree.Sha,
                request.FolderPath,
                preview.CandidateReadme,
                preview.CandidateMetadata,
                ct);
            var commit = await CreateCommitAsync(
                settings,
                $"Prepare Bethuya event archive ({request.IdempotencyKey})\nBethuya-Archive-Tree: {tree.Sha}",
                tree.Sha,
                parentSha,
                ct);
            var currentPullRequest = await FindOpenPullRequestAsync(settings, branchName, existingBranch is not null, ct);
            EnsureDraftPullRequest(currentPullRequest);
            if (pullRequest is not null && currentPullRequest is null)
            {
                throw new ArchivePreviewMismatchException(
                    "The archive Draft PR was closed during publication. Resolve its status through human review, then re-preview and confirm again.");
            }

            await UpdateBranchAsync(settings, branchName, commit.Sha, existingBranch, ct);
            pullRequest = currentPullRequest;
        }

        pullRequest ??= await CreateDraftPullRequestAsync(settings, request, branchName, ct);

        var folderUrl = $"https://github.com/{settings.Owner}/{settings.Repository}/tree/{settings.Branch}/{request.FolderPath}";
        var metadataUrl = $"https://github.com/{settings.Owner}/{settings.Repository}/blob/{settings.Branch}/{request.FolderPath}/event.yml";
        return new(folderUrl, metadataUrl, branchName, pullRequest.Number, pullRequest.HtmlUrl);
    }

    private static void EnsureDraftPullRequest(GitHubPullRequest? pullRequest)
    {
        if (pullRequest is { Draft: false })
        {
            throw new ArchivePreviewMismatchException(
                "The archive PR is no longer Draft. Complete human review or explicitly return it to Draft, then re-preview and confirm again. "
                + "Bethuya will not change a ready PR branch.");
        }
    }

    public async Task<EventArchivePullRequestStatus> GetPublicationStatusAsync(
        int pullRequestNumber,
        string folderPath,
        CancellationToken ct = default)
    {
        var settings = ConfigureClient();
        using var response = await httpClient.GetAsync(
            $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/pulls/{pullRequestNumber}",
            ct);
        response.EnsureSuccessStatusCode();
        var pullRequest = await response.Content.ReadFromJsonAsync<GitHubPullRequest>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty pull request response.");
        var folderUrl = $"https://github.com/{settings.Owner}/{settings.Repository}/tree/{settings.Branch}/{folderPath}";

        return new(
            pullRequest.Draft,
            pullRequest.MergedAt is not null,
            pullRequest.State.Equals("closed", StringComparison.OrdinalIgnoreCase),
            pullRequest.Number,
            pullRequest.HtmlUrl,
            folderUrl);
    }

    private GitHubEventRepositoryOptions ConfigureClient()
    {
        var settings = options.Value;
        var token = settings.Token ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("GitHubEvents:Token or GITHUB_TOKEN must be configured to publish event artifacts.");
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Bethuya-EventPublisher/1.0");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return settings;
    }

    private async Task<GitHubFile?> GetFileAsync(
        GitHubEventRepositoryOptions settings,
        string path,
        string branch,
        CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"{CreateContentsUri(settings, path)}?ref={Uri.EscapeDataString(branch)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var file = await response.Content.ReadFromJsonAsync<GitHubContentResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"GitHub returned an empty response for archive file '{path}'.");
        if (!file.Encoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"GitHub returned unsupported content encoding for archive file '{path}'.");
        }

        var bytes = Convert.FromBase64String(string.Concat(file.Content.Where(character => !char.IsWhiteSpace(character))));
        return new GitHubFile(Encoding.UTF8.GetString(bytes), file.Sha);
    }

    private async Task<EventArchiveEntry?> GetArchiveEntryAtRefAsync(
        GitHubEventRepositoryOptions settings,
        string folderPath,
        string branch,
        CancellationToken ct)
    {
        var readmeTask = GetFileAsync(settings, $"{folderPath}/README.md", branch, ct);
        var metadataTask = GetFileAsync(settings, $"{folderPath}/event.yml", branch, ct);
        await Task.WhenAll(readmeTask, metadataTask);
        var readme = await readmeTask;
        var metadata = await metadataTask;
        if (readme is null && metadata is null)
        {
            return null;
        }

        return new EventArchiveEntry(readme?.Content, metadata?.Content, readme?.Sha, metadata?.Sha);
    }

    private async Task<GitHubReference?> GetRefAsync(GitHubEventRepositoryOptions settings, string branch, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(CreateRefUri(settings, branch), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubReference>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"GitHub returned an empty reference for branch '{branch}'.");
    }

    private async Task<GitHubCommitResponse> GetCommitAsync(
        GitHubEventRepositoryOptions settings,
        string commitSha,
        CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(
            $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/git/commits/{Uri.EscapeDataString(commitSha)}",
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubCommitResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty commit response.");
    }

    private async Task<GitHubTreeResponse> CreateTreeAsync(
        GitHubEventRepositoryOptions settings,
        string baseTreeSha,
        string folderPath,
        string readme,
        string metadata,
        CancellationToken ct)
    {
        var request = new GitHubTreeRequest(
            baseTreeSha,
            [
                new($"{folderPath}/README.md", readme),
                new($"{folderPath}/event.yml", metadata)
            ]);
        using var response = await httpClient.PostAsJsonAsync(
            $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/git/trees",
            request,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubTreeResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty tree response.");
    }

    private async Task<GitHubCommit> CreateCommitAsync(
        GitHubEventRepositoryOptions settings,
        string message,
        string treeSha,
        string parentSha,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/git/commits",
            new GitHubCommitRequest(message, treeSha, [parentSha]),
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubCommit>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty created-commit response.");
    }

    private async Task UpdateBranchAsync(
        GitHubEventRepositoryOptions settings,
        string branchName,
        string commitSha,
        GitHubReference? existing,
        CancellationToken ct)
    {
        if (existing is null)
        {
            using var createResponse = await httpClient.PostAsJsonAsync(
                $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/git/refs",
                new GitHubCreateReference($"refs/heads/{branchName}", commitSha),
                ct);
            createResponse.EnsureSuccessStatusCode();
            return;
        }

        using var updateResponse = await httpClient.PatchAsJsonAsync(
            CreateRefUri(settings, branchName, forUpdate: true),
            new GitHubUpdateReference(commitSha, false),
            ct);
        if (updateResponse.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            throw new ArchivePreviewMismatchException(
                "GitHub refused the non-forced archive branch update; its head may have advanced concurrently. "
                + "Review and preserve the PR branch changes, then re-preview and confirm again. No forced overwrite was attempted.");
        }

        updateResponse.EnsureSuccessStatusCode();
    }

    private async Task<GitHubPullRequest?> FindOpenPullRequestAsync(
        GitHubEventRepositoryOptions settings,
        string branchName,
        bool hasExistingBranch,
        CancellationToken ct)
    {
        var head = Uri.EscapeDataString($"{settings.Owner}:{branchName}");
        var endpoint = $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/pulls";
        using var openResponse = await httpClient.GetAsync($"{endpoint}?state=open&head={head}&per_page=2", ct);
        openResponse.EnsureSuccessStatusCode();
        var openPullRequests = await openResponse.Content.ReadFromJsonAsync<GitHubPullRequest[]>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty pull request list.");
        if (openPullRequests.Length > 1
            || openPullRequests.Any(pr => pr.Base?.Ref != settings.Branch))
        {
            throw new ArchivePreviewMismatchException("The archive branch has multiple or retargeted open PRs. Resolve their human review and configured target before re-previewing; the branch was not changed.");
        }

        if (openPullRequests.Length == 1)
        {
            return openPullRequests[0];
        }

        var uri = $"{endpoint}?state=all&head={head}&sort=created&direction=desc&per_page=1";
        using var response = await httpClient.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
        var pullRequests = await response.Content.ReadFromJsonAsync<GitHubPullRequest[]>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty pull request history.");
        var latest = pullRequests.FirstOrDefault();
        if (latest is { State: "open" } && latest.Base?.Ref != settings.Branch)
        {
            throw new ArchivePreviewMismatchException("The archive PR was retargeted during publication. Resolve its configured target through human review before reconfirming.");
        }
        if (latest is { State: "closed", MergedAt: null })
        {
            throw new ArchivePreviewMismatchException(
                "The previous archive proposal was closed without merge. Resolve that human decision and explicitly reopen it as Draft before reconfirming; Bethuya will not recreate a rejected proposal.");
        }

        if (latest?.MergedAt is not null && hasExistingBranch)
        {
            throw new ArchivePreviewMismatchException(
                "The previous archive PR was merged but its publication branch still exists. Preserve it through human review and have an organizer retire it, then re-preview and confirm; Bethuya will not reset or reuse a merged branch.");
        }

        return latest is { State: "open" } ? latest : null;
    }

    private async Task<GitHubPullRequest> CreateDraftPullRequestAsync(
        GitHubEventRepositoryOptions settings,
        EventPublicationRequest request,
        string branchName,
        CancellationToken ct)
    {
        var body = """
            This draft proposes a Bethuya-owned event archive projection for human review.

            Bethuya updates only `title`, `date`, `status`, `description`, its `bethuya` identity/projection fields, and the marked README section. Other event metadata and README content are preserved. Any unmarked content whose ownership is unclear is left unchanged.

            The archive token requires repository contents and pull request permissions. This PR is never merged automatically.
            """;
        using var response = await httpClient.PostAsJsonAsync(
            $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/pulls",
            new GitHubCreatePullRequest(
                $"Update event archive: {request.Title}",
                body,
                branchName,
                settings.Branch,
                true),
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubPullRequest>(cancellationToken: ct)
            ?? throw new InvalidOperationException("GitHub returned an empty pull request response.");
    }

    private static string CreateContentsUri(GitHubEventRepositoryOptions settings, string path)
        => $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/contents/{Uri.EscapeDataString(path).Replace("%2F", "/", StringComparison.Ordinal)}";

    private static string CreateRefUri(GitHubEventRepositoryOptions settings, string branch, bool forUpdate = false)
        => $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/git/{(forUpdate ? "refs" : "ref")}/heads/{Uri.EscapeDataString(branch).Replace("%2F", "/", StringComparison.Ordinal)}";

    private sealed record GitHubFile(string Content, string Sha);

    private sealed record GitHubContentResponse(
        [property: JsonPropertyName("sha")] string Sha,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("encoding")] string Encoding);

    private sealed record GitHubReference([property: JsonPropertyName("object")] GitHubGitObject Object);
    private sealed record GitHubGitObject([property: JsonPropertyName("sha")] string Sha);
    private sealed record GitHubCommitResponse(
        [property: JsonPropertyName("tree")] GitHubGitObject Tree,
        [property: JsonPropertyName("message")] string Message);
    private sealed record GitHubTreeResponse([property: JsonPropertyName("sha")] string Sha);
    private sealed record GitHubCommit([property: JsonPropertyName("sha")] string Sha);
    private sealed record GitHubTreeRequest(
        [property: JsonPropertyName("base_tree")] string BaseTree,
        [property: JsonPropertyName("tree")] IReadOnlyCollection<GitHubTreeEntry> Tree);
    private sealed record GitHubTreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("content")] string Content)
    {
        public GitHubTreeEntry(string path, string content) : this(path, "100644", "blob", content)
        {
        }
    }

    private sealed record GitHubCommitRequest(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("tree")] string Tree,
        [property: JsonPropertyName("parents")] IReadOnlyCollection<string> Parents);
    private sealed record GitHubCreateReference(
        [property: JsonPropertyName("ref")] string Ref,
        [property: JsonPropertyName("sha")] string Sha);
    private sealed record GitHubUpdateReference(
        [property: JsonPropertyName("sha")] string Sha,
        [property: JsonPropertyName("force")] bool Force);
    private sealed record GitHubCreatePullRequest(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("head")] string Head,
        [property: JsonPropertyName("base")] string Base,
        [property: JsonPropertyName("draft")] bool Draft);
    private sealed record GitHubPullRequest(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("merged_at")] DateTimeOffset? MergedAt,
        [property: JsonPropertyName("base")] GitHubPullRequestBase? Base);
    private sealed record GitHubPullRequestBase([property: JsonPropertyName("ref")] string Ref);
}
