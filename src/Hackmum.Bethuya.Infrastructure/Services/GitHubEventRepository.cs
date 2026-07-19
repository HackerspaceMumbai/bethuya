using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Core.Services;
using Microsoft.Extensions.Options;

namespace Hackmum.Bethuya.Infrastructure.Services;

public sealed class GitHubEventRepository(HttpClient httpClient, IOptions<GitHubEventRepositoryOptions> options)
    : IGitHubEventRepository
{
    public async Task<EventPublicationResult> PublishEventAsync(EventPublicationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var token = settings.Token ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("GitHubEvents:Token or GITHUB_TOKEN must be configured to publish event artifacts.");
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Bethuya-EventPublisher/1.0");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        await UpsertFileAsync(settings, $"{request.FolderPath}/README.md", request.ReadmeMarkdown, request.IdempotencyKey, ct);
        await UpsertFileAsync(settings, $"{request.FolderPath}/metadata.json", request.MetadataJson, request.IdempotencyKey, ct);

        var folderUrl = $"https://github.com/{settings.Owner}/{settings.Repository}/tree/{settings.Branch}/{request.FolderPath}";
        var metadataUrl = $"https://github.com/{settings.Owner}/{settings.Repository}/blob/{settings.Branch}/{request.FolderPath}/metadata.json";
        return new EventPublicationResult(folderUrl, metadataUrl);
    }

    private async Task UpsertFileAsync(
        GitHubEventRepositoryOptions settings,
        string path,
        string content,
        string idempotencyKey,
        CancellationToken ct)
    {
        var existingSha = await GetExistingShaAsync(settings, path, ct);
        var requestUri = CreateContentsUri(settings, path);
        var body = new GitHubContentUpdateRequest(
            Message: $"Publish Bethuya event artifacts ({idempotencyKey})",
            Content: Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            Branch: settings.Branch,
            Sha: existingSha);

        using var response = await httpClient.PutAsJsonAsync(requestUri, body, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string?> GetExistingShaAsync(
        GitHubEventRepositoryOptions settings,
        string path,
        CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"{CreateContentsUri(settings, path)}?ref={Uri.EscapeDataString(settings.Branch)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var existing = await response.Content.ReadFromJsonAsync<GitHubContentResponse>(cancellationToken: ct);
        return existing?.Sha;
    }

    private static string CreateContentsUri(GitHubEventRepositoryOptions settings, string path)
        => $"/repos/{Uri.EscapeDataString(settings.Owner)}/{Uri.EscapeDataString(settings.Repository)}/contents/{Uri.EscapeDataString(path).Replace("%2F", "/", StringComparison.Ordinal)}";

    private sealed record GitHubContentUpdateRequest(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("branch")] string Branch,
        [property: JsonPropertyName("sha")] string? Sha);

    private sealed record GitHubContentResponse([property: JsonPropertyName("sha")] string Sha);
}
