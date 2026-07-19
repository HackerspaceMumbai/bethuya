using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Hackmum.Bethuya.Tests.Domain;

public sealed class AgendaSessionAssetTests
{
    [Test]
    public async Task ValidateSchedule_EndBeforeStart_Throws()
    {
        var session = new AgendaSession
        {
            Title = "Broken schedule",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(13, 0)
        };

        var action = session.ValidateSchedule;

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task MarkPendingUpload_MissingRequiredAssets_ReportsSlidesAndRecording()
    {
        var session = new AgendaSession { Title = "Keynote" };

        session.MarkPendingUpload(new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero));

        await Assert.That(session.AssetStatus).IsEqualTo(SessionAssetStatus.PendingUpload);
        await Assert.That(session.MissingRequiredAssets()).IsEquivalentTo(["slides", "recording"]);
        await Assert.That(session.AssetUploadCompletionPercent()).IsEqualTo(0);
    }

    [Test]
    public async Task MarkAssetsUploaded_WithHttpsUrls_CompletesAssetCollection()
    {
        var session = new AgendaSession { Title = "Workshop" };
        session.MarkPendingUpload(DateTimeOffset.UtcNow.AddDays(7));

        session.MarkAssetsUploaded(
            "https://github.com/HackerspaceMumbai/events/slides.pdf",
            "https://github.com/HackerspaceMumbai/events/recording.mp4");

        await Assert.That(session.AssetStatus).IsEqualTo(SessionAssetStatus.UploadedToVault);
        await Assert.That(session.MissingRequiredAssets()).IsEmpty();
        await Assert.That(session.AssetUploadCompletionPercent()).IsEqualTo(1);
    }

    [Test]
    public async Task MarkAssetsUploaded_WithNonHttpsUrl_Throws()
    {
        var session = new AgendaSession { Title = "Workshop" };

        var action = () => session.MarkAssetsUploaded(
            "http://example.test/slides.pdf",
            "https://example.test/recording.mp4");

        await Assert.That(action).Throws<ArgumentException>();
    }

    [Test]
    public async Task IsAssetCollectionOverdue_WhenDueDatePassedAndMissingAssets_ReturnsTrue()
    {
        var dueAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var session = new AgendaSession { Title = "Talk" };
        session.MarkPendingUpload(dueAt);

        var isOverdue = session.IsAssetCollectionOverdue(dueAt.AddSeconds(1));

        await Assert.That(isOverdue).IsTrue();
    }
}
