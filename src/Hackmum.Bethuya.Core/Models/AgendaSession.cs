using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Models;

public sealed class AgendaSession
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AgendaId { get; init; }
    public required string Title { get; set; }
    public string? Speaker { get; set; }
    public string? SpeakerGitHubHandle { get; set; }
    public string? SpeakerTwitterHandle { get; set; }
    public string? SpeakerAvatarUrl { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public SessionSource Source { get; set; } = SessionSource.ManualEntry;
    public SessionAssetStatus AssetStatus { get; private set; } = SessionAssetStatus.AwaitingEvent;
    public string? SourceSessionId { get; set; }
    public string? SlidesUrl { get; private set; }
    public string? RecordingUrl { get; private set; }
    public DateTimeOffset? AssetsDueAt { get; private set; }

    public TimeSpan? Duration
    {
        get
        {
            if (ScheduledStartAt is not null && ScheduledEndAt is not null)
            {
                return ScheduledEndAt.Value - ScheduledStartAt.Value;
            }

            if (EndTime <= StartTime)
            {
                return null;
            }

            return EndTime - StartTime;
        }
    }

    public IReadOnlyList<string> MissingRequiredAssets()
    {
        if (AssetStatus is SessionAssetStatus.AwaitingEvent or SessionAssetStatus.NoAssetsRequired or SessionAssetStatus.UploadedToVault)
        {
            return [];
        }

        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(SlidesUrl))
        {
            missing.Add("slides");
        }

        if (string.IsNullOrWhiteSpace(RecordingUrl))
        {
            missing.Add("recording");
        }

        return missing;
    }

    public double AssetUploadCompletionPercent()
    {
        if (AssetStatus is SessionAssetStatus.NoAssetsRequired or SessionAssetStatus.UploadedToVault)
        {
            return 1;
        }

        if (AssetStatus is SessionAssetStatus.AwaitingEvent)
        {
            return 0;
        }

        var uploaded = 0;
        if (!string.IsNullOrWhiteSpace(SlidesUrl))
        {
            uploaded++;
        }

        if (!string.IsNullOrWhiteSpace(RecordingUrl))
        {
            uploaded++;
        }

        return uploaded / 2d;
    }

    public bool IsAssetCollectionOverdue(DateTimeOffset now)
        => AssetStatus == SessionAssetStatus.PendingUpload
            && AssetsDueAt is not null
            && now > AssetsDueAt.Value
            && MissingRequiredAssets().Count > 0;

    public void ValidateSchedule()
    {
        var duration = Duration;
        if (duration is null || duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Session duration must be greater than zero.");
        }
    }

    public void MarkPendingUpload(DateTimeOffset? dueAt)
    {
        if (AssetStatus != SessionAssetStatus.AwaitingEvent)
        {
            return;
        }

        AssetStatus = SessionAssetStatus.PendingUpload;
        AssetsDueAt = dueAt;
    }

    public void MarkAssetsUploaded(string slidesUrl, string recordingUrl)
    {
        if (!IsHttpsUrl(slidesUrl) || !IsHttpsUrl(recordingUrl))
        {
            throw new ArgumentException("Session asset URLs must be absolute HTTPS URLs.");
        }

        SlidesUrl = slidesUrl;
        RecordingUrl = recordingUrl;
        AssetStatus = SessionAssetStatus.UploadedToVault;
    }

    public void MarkNoAssetsRequired()
    {
        AssetStatus = SessionAssetStatus.NoAssetsRequired;
        SlidesUrl = null;
        RecordingUrl = null;
        AssetsDueAt = null;
    }

    private static bool IsHttpsUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
}
