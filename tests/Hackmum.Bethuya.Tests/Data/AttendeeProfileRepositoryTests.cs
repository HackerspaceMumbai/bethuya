using Hackmum.Bethuya.Core.Models;
using Hackmum.Bethuya.Infrastructure.Data;
using Hackmum.Bethuya.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hackmum.Bethuya.Tests.Data;

public sealed class AttendeeProfileRepositoryTests
{
    [Test]
    public async Task GetInclusionSourceByEmailAsync_MatchesEmailCaseInsensitively()
    {
        await using var db = CreateDbContext();
        db.AttendeeProfiles.Add(CreateProfile("user-alice", "Alice@Example.com", languageProficiency: "Marathi, English"));
        await db.SaveChangesAsync();
        var repository = new AttendeeProfileRepository(db);

        var source = await repository.GetInclusionSourceByEmailAsync("alice@example.com");

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.LanguageProficiency).IsEqualTo("Marathi, English");
    }

    [Test]
    public async Task GetInclusionSourceByEmailAsync_SeveralMatches_ReturnsLatestUpdatedProfile()
    {
        await using var db = CreateDbContext();
        var newer = CreateProfile("user-alice-new", "Alice@Example.com", languageProficiency: "Marathi");
        newer.UpdatedAt = DateTimeOffset.UtcNow;
        var older = CreateProfile("user-alice-old", "alice@example.com", languageProficiency: "English");
        older.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30);
        db.AttendeeProfiles.AddRange(older, newer);
        await db.SaveChangesAsync();
        var repository = new AttendeeProfileRepository(db);

        var source = await repository.GetInclusionSourceByEmailAsync("ALICE@example.com");

        await Assert.That(source!.LanguageProficiency).IsEqualTo("Marathi");
    }

    [Test]
    public async Task GetInclusionSourceByEmailAsync_SameUpdatedAt_ReturnsHighestId()
    {
        await using var db = CreateDbContext();
        var updatedAt = DateTimeOffset.UtcNow;
        var higherId = CreateProfile("user-alice-b", "Alice@Example.com", languageProficiency: "Konkani",
            id: Guid.Parse("00000000-0000-7000-8000-000000000002"));
        higherId.UpdatedAt = updatedAt;
        var lowerId = CreateProfile("user-alice-a", "alice@example.com", languageProficiency: "Hindi",
            id: Guid.Parse("00000000-0000-7000-8000-000000000001"));
        lowerId.UpdatedAt = updatedAt;
        db.AttendeeProfiles.AddRange(lowerId, higherId);
        await db.SaveChangesAsync();
        var repository = new AttendeeProfileRepository(db);

        var source = await repository.GetInclusionSourceByEmailAsync("alice@example.com");

        await Assert.That(source!.LanguageProficiency).IsEqualTo("Konkani");
    }

    [Test]
    public async Task GetPublicSummariesByEmailAsync_MatchesAndGroupsEmailsCaseInsensitively()
    {
        await using var db = CreateDbContext();
        var older = CreateProfile("user-alice-old", "alice@example.com", companyName: "Old Co");
        older.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var newer = CreateProfile("user-alice-new", "Alice@Example.com", companyName: "New Co");
        newer.UpdatedAt = DateTimeOffset.UtcNow;
        db.AttendeeProfiles.AddRange(older, newer);
        await db.SaveChangesAsync();
        var repository = new AttendeeProfileRepository(db);

        var summaries = await repository.GetPublicSummariesByEmailAsync(["ALICE@example.com"]);

        await Assert.That(summaries.Count).IsEqualTo(1);
        await Assert.That(summaries.TryGetValue("alice@example.com", out var summary)).IsTrue();
        await Assert.That(summary!.CompanyName).IsEqualTo("New Co");
    }

    private static AttendeeProfile CreateProfile(
        string userId,
        string email,
        string? languageProficiency = null,
        string? companyName = null,
        Guid? id = null)
        => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            UserId = userId,
            FirstName = "Alice",
            LastName = "Fernandes",
            Email = email,
            GovernmentPhotoIdType = "PAN",
            GovernmentIdLastFour = "1234",
            LinkedInMemberId = $"{userId}-linkedin",
            GitHubLogin = $"{userId}-github",
            GitHubProfileUrl = $"https://github.com/{userId}-github",
            LanguageProficiency = languageProficiency,
            CompanyName = companyName
        };

    private static BethuyaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BethuyaDbContext>()
            .UseInMemoryDatabase($"attendee-profile-repository-tests-{Guid.NewGuid():N}")
            .Options;

        return new BethuyaDbContext(options);
    }
}
