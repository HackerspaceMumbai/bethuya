using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Hackmum.Bethuya.Core.ValueObjects;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Tests.Services;

public sealed class GitHubEventRepositoryTests
{
    [Test]
    public async Task PreviewEventAsync_ExistingArchive_PreservesCommunityContentWithoutWrites()
    {
        const string existingReadme = "# Community notes\n\nA community-owned introduction.\n";
        const string existingMetadata = """
            title: "Old title"
            slug: "community-slug"
            date: 2026-11-28
            city: "Mumbai"
            country: India
            eventType: meetup
            venue: "Community Hall"
            status: upcoming
            timezone: Asia/Kolkata
            description: "Old description"
            community: "Community-owned value"
            """;
        var handler = new FakeGitHubHandler(existingReadme, existingMetadata);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);

        var preview = await repository.PreviewEventAsync(CreateRequest());

        await Assert.That(handler.Requests.Count).IsEqualTo(3);
        await Assert.That(handler.Requests.All(request => request.Method == HttpMethod.Get)).IsTrue();
        await Assert.That(handler.Requests
            .Where(request => request.PathAndQuery.Contains("/contents/", StringComparison.Ordinal))
            .All(request => request.PathAndQuery.Contains("?ref=base-commit", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(preview.CandidateReadme).Contains("A community-owned introduction.");
        await Assert.That(preview.CandidateReadme).Contains("<!-- bethuya:managed:start -->");
        await Assert.That(preview.CandidateMetadata).Contains("city: \"Mumbai\"");
        await Assert.That(preview.CandidateMetadata).Contains("venue: \"Community Hall\"");
        await Assert.That(preview.CandidateMetadata).Contains("community: \"Community-owned value\"");
        await Assert.That(preview.CandidateMetadata).Contains("title: \"Community Summit\"");
    }

    [Test]
    public async Task PublishEventAsync_CreatesDeterministicBranchAndDraftPullRequest()
    {
        var handler = new FakeGitHubHandler(null, null);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var request = await ConfirmPreviewAsync(repository, CreateRequest());

        var result = await repository.PublishEventAsync(request);
        var expectedBranch = $"bethuya/event-archive-{request.EventId.Value:N}";

        await Assert.That(result.BranchName).IsEqualTo(expectedBranch);
        await Assert.That(result.PullRequestNumber).IsEqualTo(42);
        await Assert.That(handler.CreatedPullRequests).IsEqualTo(1);
        await Assert.That(handler.CreatedPullRequestDraft).IsTrue();
        await Assert.That(handler.CreatedBranchRef).IsEqualTo($"refs/heads/{expectedBranch}");
        await Assert.That(handler.CreatedTreeCount).IsEqualTo(1);
        await Assert.That(handler.Requests
            .Where(request => request.PathAndQuery.Contains("/contents/", StringComparison.Ordinal))
            .All(request => request.PathAndQuery.Contains("?ref=base-commit", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task PublishEventAsync_ExistingDraftPullRequest_UpdatesItsBranchAndReusesPullRequest()
    {
        var handler = new FakeGitHubHandler(null, null);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var request = await ConfirmPreviewAsync(repository, CreateRequest());

        await repository.PublishEventAsync(request);
        handler.SetBaseArchive(
            "# Community notes\n\nNew community note.\n",
            """
            title: "Old title"
            slug: "community-slug"
            date: 2026-11-28
            city: "Mumbai"
            country: India
            eventType: meetup
            venue: "Community-managed venue"
            status: upcoming
            timezone: Asia/Kolkata
            description: "Old description"
            community: "Community-owned value"
            """);

        var updatedRequest = request with
        {
            Title = "Updated Community Summit",
            ReadmeMarkdown = "# Updated Community Summit\nLifecycle: Published\n",
            MetadataJson = request.MetadataJson.Replace(
                "title: \"Community Summit\"",
                "title: \"Updated Community Summit\"",
                StringComparison.Ordinal),
            IdempotencyKey = "updated-projection-0123456789ABCDEF"
        };
        updatedRequest = await ConfirmPreviewAsync(repository, updatedRequest);
        var updateResult = await repository.PublishEventAsync(updatedRequest);

        await Assert.That(updateResult.PullRequestNumber).IsEqualTo(42);
        await Assert.That(updateResult.BranchName).IsEqualTo(
            $"bethuya/event-archive-{request.EventId.Value:N}");
        await Assert.That(handler.CreatedPullRequests).IsEqualTo(1);
        await Assert.That(handler.UpdatedBranchCount).IsEqualTo(1);
        await Assert.That(handler.CreatedTreeCount).IsEqualTo(2);
        await Assert.That(handler.LastTreeMetadata).Contains("venue: \"Community-managed venue\"");
        await Assert.That(handler.LastTreeReadme).Contains("New community note.");
    }

    [Test]
    public async Task PublishEventAsync_ChangedProjectionWithUnchangedTarget_UpdatesExistingDraftBranch()
    {
        var handler = new FakeGitHubHandler(
            "# Community notes\n\nA community-owned introduction.\n",
            """
            title: "Old title"
            slug: "community-slug"
            date: 2026-11-28
            city: "Mumbai"
            country: India
            eventType: meetup
            venue: "Community Hall"
            status: upcoming
            timezone: Asia/Kolkata
            description: "Old description"
            community: "Community-owned value"
            """);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var request = await ConfirmPreviewAsync(repository, CreateRequest());

        await repository.PublishEventAsync(request);

        var updatedRequest = request with
        {
            Title = "Updated Community Summit",
            ReadmeMarkdown = "# Updated Community Summit\nLifecycle: Published\n",
            MetadataJson = request.MetadataJson
                .Replace("title: \"Community Summit\"", "title: \"Updated Community Summit\"", StringComparison.Ordinal)
                .Replace("description: \"Summit\"", "description: \"Updated summit\"", StringComparison.Ordinal),
            IdempotencyKey = "updated-projection-0123456789ABCDEF"
        };
        var updatedPreview = await repository.PreviewEventAsync(updatedRequest);
        await Assert.That(EventArchiveContentMerger.MergeReadme(handler.LastTreeReadme, updatedRequest.ReadmeMarkdown))
            .IsEqualTo(updatedPreview.CandidateReadme);
        await Assert.That(EventArchiveContentMerger.MergeMetadata(handler.LastTreeMetadata, updatedRequest.MetadataJson))
            .IsEqualTo(updatedPreview.CandidateMetadata);
        updatedRequest = await ConfirmPreviewAsync(repository, updatedRequest);
        var updateResult = await repository.PublishEventAsync(updatedRequest);

        await Assert.That(updateResult.PullRequestNumber).IsEqualTo(42);
        await Assert.That(handler.CreatedPullRequests).IsEqualTo(1);
        await Assert.That(handler.UpdatedBranchCount).IsEqualTo(1);
        await Assert.That(handler.CreatedTreeCount).IsEqualTo(2);
        await Assert.That(handler.LastTreeReadme).Contains("Updated Community Summit");
        await Assert.That(handler.LastTreeMetadata).Contains("title: \"Updated Community Summit\"");
        await Assert.That(handler.LastTreeMetadata).Contains("description: \"Updated summit\"");
    }

    [Test]
    public async Task PublishEventAsync_RetryWithUnchangedTarget_DoesNotCreateDuplicateCommitOrPullRequest()
    {
        var handler = new FakeGitHubHandler(null, null);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var request = await ConfirmPreviewAsync(repository, CreateRequest());

        await repository.PublishEventAsync(request);

        var retry = await repository.PublishEventAsync(request);

        await Assert.That(retry.PullRequestNumber).IsEqualTo(42);
        await Assert.That(handler.CreatedTreeCount).IsEqualTo(1);
        await Assert.That(handler.CreatedPullRequests).IsEqualTo(1);
    }

    [Test]
    public async Task PublishEventAsync_RetryAfterBranchUpdateAndPullRequestFailure_ReusesBranchCommit()
    {
        var handler = new FakeGitHubHandler(null, null)
        {
            FailNextPullRequestCreation = true
        };
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var request = await ConfirmPreviewAsync(repository, CreateRequest());

        var firstAttempt = async () => { await repository.PublishEventAsync(request); };
        await Assert.That(firstAttempt).Throws<HttpRequestException>();

        var retry = await repository.PublishEventAsync(request);

        await Assert.That(retry.PullRequestNumber).IsEqualTo(42);
        await Assert.That(handler.CreatedTreeCount).IsEqualTo(1);
        await Assert.That(handler.CreatedPullRequests).IsEqualTo(1);
        await Assert.That(handler.UpdatedBranchCount).IsEqualTo(0);
    }

    [Test]
    public async Task PublishEventAsync_LegacyRequestWithoutConfirmedToken_RefusesBeforeAnyGitHubRequest()
    {
        var handler = new FakeGitHubHandler(null, null);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var action = async () => { await repository.PublishEventAsync(CreateRequest()); };

        var exception = await Assert.That(action).Throws<ArchivePreviewMismatchException>();

        await Assert.That(exception!.Message).Contains("Re-preview the archive");
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("readme")]
    [Arguments("managed-metadata")]
    [Arguments("block-metadata")]
    public async Task PublishEventAsync_TargetChangesAfterConfirmation_RefusesBeforeAnyWrite(string change)
    {
        var request = CreateRequest();
        const string originalReadme = "# Community notes\nOriginal archive note.\n";
        var handler = new FakeGitHubHandler(originalReadme, request.MetadataJson);
        using var client = CreateHttpClient(handler);
        var repository = CreateRepository(client);
        var preview = await repository.PreviewEventAsync(request);
        handler.SetBaseArchive(
            change == "readme" ? "# Community notes\nA newly added archive note.\n" : originalReadme,
            change switch
            {
                "managed-metadata" => request.MetadataJson.Replace(
                    "title: \"Community Summit\"", "title: \"Community edit\"", StringComparison.Ordinal),
                "block-metadata" => request.MetadataJson.Replace(
                    "description: \"Summit\"", "description: >-\n  Community multiline edit", StringComparison.Ordinal),
                _ => request.MetadataJson
            });

        request = request with { ConfirmedPreviewToken = preview.PreviewToken };
        var action = async () => { await repository.PublishEventAsync(request); };

        await Assert.That(action).Throws<ArchivePreviewMismatchException>();
        await Assert.That(handler.Requests.All(call => call.Method == HttpMethod.Get)).IsTrue();
        await Assert.That(handler.CreatedTreeCount).IsEqualTo(0);
        await Assert.That(handler.CreatedPullRequests).IsEqualTo(0);
        await Assert.That(handler.UpdatedBranchCount).IsEqualTo(0);
    }

    private static EventPublicationRequest CreateRequest()
        => new(
            EventId.From(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            "Community Summit",
            "events/2026/2026-11-28-community-summit",
            "# Community Summit\nLifecycle: Published\n",
            """
            title: "Community Summit"
            slug: "2026-11-28-community-summit"
            date: 2026-11-28
            city: "Mumbai"
            country: India
            eventType: meetup
            series: null
            community: Hackerspace Mumbai
            venue: "TBD"
            website: https://hackmum.in
            eventPage: https://hackmum.in/past-events/
            resourcesAvailable: false
            status: upcoming
            timezone: Asia/Kolkata
            description: "Summit"
            bethuya:
              eventId: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
              projectionVersion: 1
              managedFields:
                - title
                - date
                - status
                - description
            """,
            "aaaaaaaaaaaaaaaaaaaaaaaa-0123456789ABCDEF");

    private static async Task<EventPublicationRequest> ConfirmPreviewAsync(
        GitHubEventRepository repository,
        EventPublicationRequest request)
    {
        var preview = await repository.PreviewEventAsync(request);
        return request with { ConfirmedPreviewToken = preview.PreviewToken };
    }

    private static GitHubEventRepository CreateRepository(HttpClient client)
        => new(client, Options.Create(new GitHubEventRepositoryOptions
        {
            Owner = "HackerspaceMumbai",
            Repository = "events",
            Branch = "main",
            Token = "test-token"
        }));

    private static HttpClient CreateHttpClient(FakeGitHubHandler handler)
        => new(handler) { BaseAddress = new Uri("https://api.github.com") };

    private sealed class FakeGitHubHandler(string? baseReadme, string? baseMetadata) : HttpMessageHandler
    {
        private string? _baseReadme = baseReadme;
        private string? _baseMetadata = baseMetadata;
        private string? _branchReadme;
        private string? _branchMetadata;
        private bool _branchExists;
        private bool _pullRequestExists;

        public List<(HttpMethod Method, string PathAndQuery)> Requests { get; } = [];
        public string? CreatedBranchRef { get; private set; }
        public bool CreatedPullRequestDraft { get; private set; }
        public int CreatedPullRequests { get; private set; }
        public int CreatedTreeCount { get; private set; }
        public int UpdatedBranchCount { get; private set; }
        public string? LastTreeReadme { get; private set; }
        public string? LastTreeMetadata { get; private set; }
        public bool FailNextPullRequestCreation { get; set; }

        public void SetBaseArchive(string readme, string metadata)
        {
            _baseReadme = readme;
            _baseMetadata = metadata;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var pathAndQuery = uri.PathAndQuery;
            Requests.Add((request.Method, pathAndQuery));

            if (request.Method == HttpMethod.Get && pathAndQuery.Contains("/contents/", StringComparison.Ordinal))
            {
                var refName = GetRefName(uri);
                var readme = refName is "main" or "base-commit" ? _baseReadme : _branchReadme;
                var metadata = refName is "main" or "base-commit" ? _baseMetadata : _branchMetadata;
                var content = pathAndQuery.Contains("/README.md", StringComparison.Ordinal) ? readme : metadata;
                return Task.FromResult(content is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : JsonResponse(new
                    {
                        sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
                        encoding = "base64",
                        content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content))
                    }));
            }

            if (request.Method == HttpMethod.Get && pathAndQuery.Contains("/git/ref/heads/main", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new { @object = new { sha = "base-commit" } }));
            }

            if (request.Method == HttpMethod.Get && pathAndQuery.Contains("/git/ref/heads/bethuya/", StringComparison.Ordinal))
            {
                return Task.FromResult(!_branchExists
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : JsonResponse(new { @object = new { sha = "existing-commit" } }));
            }

            if (request.Method == HttpMethod.Get && pathAndQuery.Contains("/git/commits/base-commit", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new { tree = new { sha = "base-tree" } }));
            }

            if (request.Method == HttpMethod.Post && pathAndQuery.EndsWith("/git/trees", StringComparison.Ordinal))
            {
                CreatedTreeCount++;
                var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                var tree = document.RootElement.GetProperty("tree").EnumerateArray().ToArray();
                var readmeContent = tree.Single(entry => entry.GetProperty("path").GetString()!.EndsWith("README.md", StringComparison.Ordinal))
                    .GetProperty("content").GetString()!;
                var metadataContent = tree.Single(entry => entry.GetProperty("path").GetString()!.EndsWith("event.yml", StringComparison.Ordinal))
                    .GetProperty("content").GetString()!;
                LastTreeReadme = readmeContent;
                LastTreeMetadata = metadataContent;
                _branchReadme = readmeContent;
                _branchMetadata = metadataContent;
                return Task.FromResult(JsonResponse(new { sha = $"tree-{CreatedTreeCount}" }));
            }

            if (request.Method == HttpMethod.Post && pathAndQuery.EndsWith("/git/commits", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new { sha = "new-commit" }));
            }

            if (request.Method == HttpMethod.Get && pathAndQuery.Contains("/pulls?", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(_pullRequestExists
                    ? new[] { PullRequest() }
                    : []));
            }

            if (request.Method == HttpMethod.Post && pathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal))
            {
                var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                CreatedBranchRef = document.RootElement.GetProperty("ref").GetString();
                _branchExists = true;
                return Task.FromResult(JsonResponse(new { @ref = CreatedBranchRef }));
            }

            if (request.Method == HttpMethod.Patch && pathAndQuery.Contains("/git/ref/heads/bethuya/", StringComparison.Ordinal))
            {
                UpdatedBranchCount++;
                _branchExists = true;
                return Task.FromResult(JsonResponse(new { @ref = "refs/heads/bethuya/event-archive" }));
            }

            if (request.Method == HttpMethod.Post && pathAndQuery.EndsWith("/pulls", StringComparison.Ordinal))
            {
                if (FailNextPullRequestCreation)
                {
                    FailNextPullRequestCreation = false;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                }

                CreatedPullRequests++;
                var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                CreatedPullRequestDraft = document.RootElement.GetProperty("draft").GetBoolean();
                _pullRequestExists = true;
                return Task.FromResult(JsonResponse(PullRequest()));
            }

            throw new InvalidOperationException($"Unexpected fake GitHub request: {request.Method} {pathAndQuery}");
        }

        private static string GetRefName(Uri uri)
        {
            var query = uri.Query;
            var rawValue = query.Split('=', 2)[1];
            return Uri.UnescapeDataString(rawValue);
        }

        private static object PullRequest()
            => new
            {
                number = 42,
                html_url = "https://github.com/HackerspaceMumbai/events/pull/42",
                state = "open",
                draft = true,
                merged_at = (DateTimeOffset?)null
            };

        private static HttpResponseMessage JsonResponse<T>(T value)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
            };
    }
}
