using Liftingo.Domain.Account;

using Microsoft.Extensions.Time.Testing;

namespace Liftingo.Api.UnitTests.Domain.Account;

public class EmailTokenTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Issue_ValidInput_SetsExpiryAndLeavesTokenUsable()
    {
        EmailToken token = IssueToken();

        Assert.Equal(EmailTokenPurpose.EmailVerification, token.Purpose);
        Assert.Equal(_time.GetUtcNow(), token.CreatedAt);
        Assert.Equal(_time.GetUtcNow() + Lifetime, token.ExpiresAt);
        Assert.Null(token.LoginProvider);
        Assert.Null(token.ProviderKey);
        Assert.True(token.IsUsable(_time.GetUtcNow()));
    }

    [Fact]
    public void Issue_ProviderLinkPurpose_ThrowsDomainException()
    {
        Assert.Throws<AccountDomainException>(() => EmailToken.Issue(
            Guid.NewGuid(), EmailTokenPurpose.ProviderLinkConfirmation, Hash(), _time.GetUtcNow(), Lifetime));
    }

    [Fact]
    public void Issue_HashOfWrongLength_ThrowsDomainException()
    {
        Assert.Throws<AccountDomainException>(() => EmailToken.Issue(
            Guid.NewGuid(), EmailTokenPurpose.PasswordReset, new byte[16], _time.GetUtcNow(), Lifetime));
    }

    [Fact]
    public void Issue_NonPositiveLifetime_ThrowsDomainException()
    {
        Assert.Throws<AccountDomainException>(() => EmailToken.Issue(
            Guid.NewGuid(), EmailTokenPurpose.PasswordReset, Hash(), _time.GetUtcNow(), TimeSpan.Zero));
    }

    [Fact]
    public void IssueProviderLink_ValidInput_SetsProviderFields()
    {
        EmailToken token = EmailToken.IssueProviderLink(
            Guid.NewGuid(), Hash(), "Google", "provider-key-1", _time.GetUtcNow(), Lifetime);

        Assert.Equal(EmailTokenPurpose.ProviderLinkConfirmation, token.Purpose);
        Assert.Equal("Google", token.LoginProvider);
        Assert.Equal("provider-key-1", token.ProviderKey);
    }

    [Theory]
    [InlineData(null, "key")]
    [InlineData("", "key")]
    [InlineData("Google", null)]
    [InlineData("Google", "")]
    public void IssueProviderLink_MissingProviderField_ThrowsDomainException(string? provider, string? key)
    {
        Assert.Throws<AccountDomainException>(() => EmailToken.IssueProviderLink(
            Guid.NewGuid(), Hash(), provider!, key!, _time.GetUtcNow(), Lifetime));
    }

    [Fact]
    public void IsUsable_BeforeExpiry_ReturnsTrue()
    {
        EmailToken token = IssueToken();
        _time.Advance(Lifetime - TimeSpan.FromSeconds(1));

        Assert.True(token.IsUsable(_time.GetUtcNow()));
    }

    [Fact]
    public void IsUsable_AtExpiry_ReturnsFalse()
    {
        EmailToken token = IssueToken();
        _time.Advance(Lifetime);

        Assert.False(token.IsUsable(_time.GetUtcNow()));
    }

    [Fact]
    public void IsUsable_Consumed_ReturnsFalse()
    {
        EmailToken token = IssueToken();
        token.Consume(_time.GetUtcNow());

        Assert.False(token.IsUsable(_time.GetUtcNow()));
    }

    [Fact]
    public void IsUsable_Superseded_ReturnsFalse()
    {
        EmailToken token = IssueToken();
        token.Supersede(_time.GetUtcNow());

        Assert.False(token.IsUsable(_time.GetUtcNow()));
    }

    [Fact]
    public void Consume_UsableToken_SetsConsumedAt()
    {
        EmailToken token = IssueToken();
        _time.Advance(TimeSpan.FromMinutes(5));

        token.Consume(_time.GetUtcNow());

        Assert.Equal(_time.GetUtcNow(), token.ConsumedAt);
    }

    [Fact]
    public void Consume_AlreadyConsumed_Throws()
    {
        EmailToken token = IssueToken();
        token.Consume(_time.GetUtcNow());

        Assert.Throws<AccountDomainException>(() => token.Consume(_time.GetUtcNow()));
    }

    [Fact]
    public void Consume_Superseded_Throws()
    {
        EmailToken token = IssueToken();
        token.Supersede(_time.GetUtcNow());

        Assert.Throws<AccountDomainException>(() => token.Consume(_time.GetUtcNow()));
    }

    [Fact]
    public void Consume_Expired_Throws()
    {
        EmailToken token = IssueToken();
        _time.Advance(Lifetime + TimeSpan.FromSeconds(1));

        Assert.Throws<AccountDomainException>(() => token.Consume(_time.GetUtcNow()));
        Assert.Null(token.ConsumedAt);
    }

    [Fact]
    public void UnusableSince_UsableToken_ReturnsNull()
    {
        EmailToken token = IssueToken();

        Assert.Null(token.UnusableSince(_time.GetUtcNow()));
    }

    [Fact]
    public void UnusableSince_Expired_ReturnsExpiresAt()
    {
        EmailToken token = IssueToken();
        _time.Advance(Lifetime + TimeSpan.FromHours(5));

        Assert.Equal(token.ExpiresAt, token.UnusableSince(_time.GetUtcNow()));
    }

    [Fact]
    public void UnusableSince_ConsumedBeforeExpiry_ReturnsConsumedAt()
    {
        EmailToken token = IssueToken();
        _time.Advance(TimeSpan.FromMinutes(10));
        DateTimeOffset consumedAt = _time.GetUtcNow();
        token.Consume(consumedAt);
        _time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(consumedAt, token.UnusableSince(_time.GetUtcNow()));
    }

    [Fact]
    public void UnusableSince_SupersededBeforeExpiry_ReturnsSupersededAt()
    {
        EmailToken token = IssueToken();
        _time.Advance(TimeSpan.FromMinutes(20));
        DateTimeOffset supersededAt = _time.GetUtcNow();
        token.Supersede(supersededAt);
        _time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(supersededAt, token.UnusableSince(_time.GetUtcNow()));
    }

    [Fact]
    public void UnusableSince_ConsumedAndLaterSuperseded_ReturnsEarlierOfTheTwo()
    {
        EmailToken token = IssueToken();
        _time.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset consumedAt = _time.GetUtcNow();
        token.Consume(consumedAt);
        _time.Advance(TimeSpan.FromMinutes(5));
        token.Supersede(_time.GetUtcNow());

        Assert.Equal(consumedAt, token.UnusableSince(_time.GetUtcNow()));
    }

    [Fact]
    public void UnusableSince_QueriedBeforeExpiry_IgnoresTheFutureExpiry()
    {
        EmailToken token = IssueToken();
        _time.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset supersededAt = _time.GetUtcNow();
        token.Supersede(supersededAt);

        Assert.Equal(supersededAt, token.UnusableSince(supersededAt));
    }

    private static byte[] Hash() => new byte[EmailToken.TokenHashLength];

    private EmailToken IssueToken() =>
        EmailToken.Issue(Guid.NewGuid(), EmailTokenPurpose.EmailVerification, Hash(), _time.GetUtcNow(), Lifetime);
}