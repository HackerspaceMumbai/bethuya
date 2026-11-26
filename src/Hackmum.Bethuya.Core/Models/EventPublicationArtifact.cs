using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Core.Models;

/// <summary>
/// Deterministic publication artifact generated for an event.
/// </summary>
public sealed record EventPublicationArtifact(
    string FolderPath,
    string ReadmeMarkdown,
    string MetadataJson);

/// <summary>
/// Publication request sent to the GitHub publishing port.
/// </summary>
public sealed record EventPublicationRequest(
    EventId EventId,
    string Title,
    string FolderPath,
    string ReadmeMarkdown,
    string MetadataJson,
    string IdempotencyKey,
    string? ConfirmedPreviewToken = null);

/// <summary>Archive entry read from the configured repository.</summary>
public sealed record EventArchiveEntry(
    string? ReadmeMarkdown,
    string? MetadataYaml,
    string? ReadmeSha,
    string? MetadataSha);

/// <summary>Read-only archive preview, including the exact candidate files for organizer review.</summary>
public sealed record EventArchivePreview(
    string FolderPath,
    string PreviewToken,
    string? ExistingReadme,
    string CandidateReadme,
    string? ExistingMetadata,
    string CandidateMetadata,
    bool HasChanges);

/// <summary>
/// Result returned by the GitHub publishing port.
/// </summary>
public sealed record EventPublicationResult(
    string FolderUrl,
    string MetadataUrl,
    string BranchName,
    int PullRequestNumber,
    string PullRequestUrl);

/// <summary>Current state of a publication pull request, read on organizer request.</summary>
public sealed record EventArchivePullRequestStatus(
    bool IsDraft,
    bool IsMerged,
    bool IsClosed,
    int PullRequestNumber,
    string PullRequestUrl,
    string FolderUrl);

/// <summary>Organizer-facing archive publication preview for an event.</summary>
public sealed record EventArchivePublicationPreview(
    Guid EventId,
    EventArchivePreview Preview);

/// <summary>Result of explicitly adopting a reviewed archive entry.</summary>
public sealed record EventArchiveAdoptionResult(
    Guid EventId,
    string FolderPath,
    string Actor,
    DateTimeOffset AdoptedAt);
