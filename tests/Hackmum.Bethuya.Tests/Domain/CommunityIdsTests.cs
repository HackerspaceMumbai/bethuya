using Hackmum.Bethuya.Core.ValueObjects;

namespace Hackmum.Bethuya.Tests.Domain;

public class CommunityIdsTests
{
    [Test]
    public async Task GraphNodeId_ValidatesValue()
    {
        const string value = "member:123";

        await Assert.That(GraphNodeId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => GraphNodeId.From(string.Empty));
        Assert.Throws<Vogen.ValueObjectValidationException>(() => GraphNodeId.From("   "));
    }

    [Test]
    public async Task CommunityMemberId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(CommunityMemberId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => CommunityMemberId.From(Guid.Empty));
    }

    [Test]
    public async Task ExternalIdentityId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(ExternalIdentityId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => ExternalIdentityId.From(Guid.Empty));
    }

    [Test]
    public async Task EventId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(EventId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => EventId.From(Guid.Empty));
    }

    [Test]
    public async Task ParticipationLedgerEntryId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(ParticipationLedgerEntryId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => ParticipationLedgerEntryId.From(Guid.Empty));
    }

    [Test]
    public async Task MentorProfileId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(MentorProfileId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => MentorProfileId.From(Guid.Empty));
    }

    [Test]
    public async Task RegistrationId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(RegistrationId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => RegistrationId.From(Guid.Empty));
    }

    [Test]
    public async Task EventArchiveOutboxMessageId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(EventArchiveOutboxMessageId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => EventArchiveOutboxMessageId.From(Guid.Empty));
    }

    [Test]
    public async Task CommunityPortfolioEntryId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(CommunityPortfolioEntryId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => CommunityPortfolioEntryId.From(Guid.Empty));
    }

    [Test]
    public async Task MemberOpportunityId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(MemberOpportunityId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => MemberOpportunityId.From(Guid.Empty));
    }

    [Test]
    public async Task CommunityRelationshipId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(CommunityRelationshipId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => CommunityRelationshipId.From(Guid.Empty));
    }

    [Test]
    public async Task CommunitySignalAwardId_ValidatesValue()
    {
        var value = Guid.NewGuid();

        await Assert.That(CommunitySignalAwardId.From(value).Value).IsEqualTo(value);
        Assert.Throws<Vogen.ValueObjectValidationException>(() => CommunitySignalAwardId.From(Guid.Empty));
    }
}
