using Liftingo.Domain.Account;

using Microsoft.Extensions.Time.Testing;

namespace Liftingo.Api.UnitTests.Domain.Account;

public class UserConsentTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Record_ValidInput_SetsAllFieldsAndLeavesConsentCurrent()
    {
        var userId = Guid.NewGuid();

        UserConsent consent = UserConsent.Record(userId, ConsentType.HealthData, granted: true, "2026-10-01", _time.GetUtcNow());

        Assert.NotEqual(Guid.Empty, consent.Id);
        Assert.Equal(userId, consent.UserId);
        Assert.Equal(ConsentType.HealthData, consent.Type);
        Assert.True(consent.Granted);
        Assert.Equal(_time.GetUtcNow(), consent.RecordedAt);
        Assert.Equal("2026-10-01", consent.DocumentVersion);
        Assert.Null(consent.SupersededAt);
    }

    [Fact]
    public void Record_NotGranted_StoresFalse()
    {
        UserConsent consent = UserConsent.Record(Guid.NewGuid(), ConsentType.Marketing, granted: false, "v1", _time.GetUtcNow());

        Assert.False(consent.Granted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_EmptyVersion_ThrowsDomainException(string version)
    {
        Assert.Throws<AccountDomainException>(
            () => UserConsent.Record(Guid.NewGuid(), ConsentType.TermsAndAge, granted: true, version, _time.GetUtcNow()));
    }

    [Fact]
    public void Record_VersionOf32Chars_Succeeds()
    {
        UserConsent consent = UserConsent.Record(
            Guid.NewGuid(), ConsentType.TermsAndAge, granted: true, new string('a', 32), _time.GetUtcNow());

        Assert.Equal(32, consent.DocumentVersion.Length);
    }

    [Fact]
    public void Record_VersionLongerThan32Chars_ThrowsDomainException()
    {
        Assert.Throws<AccountDomainException>(
            () => UserConsent.Record(Guid.NewGuid(), ConsentType.TermsAndAge, granted: true, new string('a', 33), _time.GetUtcNow()));
    }

    [Fact]
    public void Supersede_CurrentConsent_SetsSupersededAt()
    {
        UserConsent consent = UserConsent.Record(Guid.NewGuid(), ConsentType.TermsAndAge, granted: true, "v1", _time.GetUtcNow());
        _time.Advance(TimeSpan.FromHours(1));

        consent.Supersede(_time.GetUtcNow());

        Assert.Equal(_time.GetUtcNow(), consent.SupersededAt);
    }

    [Fact]
    public void Supersede_AlreadySuperseded_ThrowsAndKeepsFirstTimestamp()
    {
        UserConsent consent = UserConsent.Record(Guid.NewGuid(), ConsentType.TermsAndAge, granted: true, "v1", _time.GetUtcNow());
        DateTimeOffset first = _time.GetUtcNow();
        consent.Supersede(first);
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Throws<AccountDomainException>(() => consent.Supersede(_time.GetUtcNow()));
        Assert.Equal(first, consent.SupersededAt);
    }
}