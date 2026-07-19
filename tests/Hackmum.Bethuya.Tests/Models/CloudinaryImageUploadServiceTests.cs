using System.Security.Cryptography;
using System.Text;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hackmum.Bethuya.Tests.Models;

public class CloudinaryImageUploadServiceTests
{
    [Test]
    public async Task CreateDirectUploadSession_GeneratesExpectedSignature()
    {
        await using var db = CreateDbContext();
        var options = CreateOptions(uploadPreset: "bethuya-signed");
        var service = CreateService(db, options);

        var session = await service.CreateDirectUploadSessionAsync("cover.png", "image/png", 2048);

        var expected = ComputeExpectedSignature(
            session.PublicId,
            session.Timestamp,
            options.ApiSecret,
            session.AllowedFormats);
        await Assert.That(session.Signature).IsEqualTo(expected);
    }

    [Test]
    public async Task DeletePendingUpload_ReturnsFalse_WhenDeleteTokenDoesNotMatch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, CreateOptions());
        var session = await service.CreateDirectUploadSessionAsync("cover.png", "image/png", 2048);

        var deleted = await service.DeletePendingUploadAsync(session.PublicId, "wrong-token");

        await Assert.That(deleted).IsFalse();
        await Assert.That(await service.IsPendingUploadAsync(session.PublicId)).IsTrue();
    }

    [Test]
    public async Task MarkUploadAttached_MakesUploadNoLongerPending()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, CreateOptions());
        var session = await service.CreateDirectUploadSessionAsync("cover.png", "image/png", 2048);

        await service.MarkUploadAttachedAsync(session.PublicId);

        await Assert.That(await service.IsPendingUploadAsync(session.PublicId)).IsFalse();
    }

    [Test]
    public async Task TryGetPublicId_ParsesExpectedCloudinaryUrl()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, CreateOptions());

        var parsed = service.TryGetPublicId(
            "https://res.cloudinary.com/demo/image/upload/v1715000000/bethuya/events/pending/test-cover.png",
            out var publicId);

        await Assert.That(parsed).IsTrue();
        await Assert.That(publicId).IsEqualTo("bethuya/events/pending/test-cover");
    }

    [Test]
    public async Task Constructor_AllowsMissingCloudinaryConfiguration()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, new CloudinaryOptions());

        var parsed = service.TryGetPublicId(
            "https://res.cloudinary.com/demo/image/upload/v1715000000/bethuya/events/pending/test-cover.png",
            out var publicId);

        await Assert.That(service).IsNotNull();
        await Assert.That(parsed).IsFalse();
        await Assert.That(publicId).IsEmpty();
    }

    [Test]
    public async Task CreateDirectUploadSession_WithoutConfiguration_ThrowsClearError()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, new CloudinaryOptions());

        Func<Task> action = () => service.CreateDirectUploadSessionAsync("cover.png", "image/png", 2048);

        var exception = await Assert.That(action).Throws<InvalidOperationException>();
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception.Message).IsEqualTo("Cloudinary image uploads are not configured.");
    }

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"cloudinary-upload-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }

    private static CloudinaryOptions CreateOptions(string? uploadPreset = null) =>
        new()
        {
            CloudName = "demo",
            ApiKey = "key123",
            ApiSecret = "secret456",
            UploadPreset = uploadPreset
        };

    private static CloudinaryImageUploadService CreateService(BethuyaDbContext db, CloudinaryOptions options) =>
        new(
            db,
            Options.Create(options),
            TimeProvider.System,
            Substitute.For<ILogger<CloudinaryImageUploadService>>());

    #pragma warning disable CA5350 // Cloudinary signed uploads require SHA1 for request signing.
    private static string ComputeExpectedSignature(
        string publicId,
        long timestamp,
        string apiSecret,
        string allowedFormats)
    {
        var signatureParts = new List<string>
        {
            $"allowed_formats={allowedFormats}",
            $"public_id={publicId}",
            $"timestamp={timestamp}"
        };

        signatureParts.Sort(StringComparer.Ordinal);
        var payload = $"{string.Join("&", signatureParts)}{apiSecret}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    #pragma warning restore CA5350
}
