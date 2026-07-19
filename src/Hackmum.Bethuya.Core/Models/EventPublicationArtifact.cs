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
    Guid EventId,
    string Title,
    string FolderPath,
    string ReadmeMarkdown,
    string MetadataJson,
    string IdempotencyKey);

/// <summary>
/// Result returned by the GitHub publishing port.
/// </summary>
public sealed record EventPublicationResult(
    string FolderUrl,
    string MetadataUrl);
