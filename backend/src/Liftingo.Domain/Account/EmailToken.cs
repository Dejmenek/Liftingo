using System.Diagnostics.CodeAnalysis;

namespace Liftingo.Domain.Account;

public sealed class EmailToken
{
    public const int TokenHashLength = 32;
    public const int LoginProviderMaxLength = 32;
    public const int ProviderKeyMaxLength = 256;

    // For EF Core.
    private EmailToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public EmailTokenPurpose Purpose { get; private set; }

    // SHA-256 of the token that was sent by e-mail.
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Mapped to a binary(32) column by EF Core.")]
    public byte[] TokenHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public DateTimeOffset? SupersededAt { get; private set; }

    // ProviderLinkConfirmation only: Google / Facebook / X.
    public string? LoginProvider { get; private set; }

    // ProviderLinkConfirmation only: the provider's user id.
    public string? ProviderKey { get; private set; }

    public static EmailToken Issue(
        Guid userId,
        EmailTokenPurpose purpose,
        byte[] tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (purpose == EmailTokenPurpose.ProviderLinkConfirmation)
        {
            throw new AccountDomainException("Token potwierdzenia powiązania konta wymaga danych dostawcy.");
        }

        return Create(userId, purpose, tokenHash, now, lifetime, loginProvider: null, providerKey: null);
    }

    public static EmailToken IssueProviderLink(
        Guid userId,
        byte[] tokenHash,
        string loginProvider,
        string providerKey,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(loginProvider) || loginProvider.Length > LoginProviderMaxLength ||
            string.IsNullOrWhiteSpace(providerKey) || providerKey.Length > ProviderKeyMaxLength)
        {
            throw new AccountDomainException("Dane dostawcy logowania są nieprawidłowe.");
        }

        return Create(userId, EmailTokenPurpose.ProviderLinkConfirmation, tokenHash, now, lifetime, loginProvider, providerKey);
    }

    public bool IsUsable(DateTimeOffset now) =>
        ConsumedAt is null && SupersededAt is null && now < ExpiresAt;

    public void Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            throw new AccountDomainException("Token nie jest już ważny.");
        }

        ConsumedAt = now;
    }

    public void Supersede(DateTimeOffset now) => SupersededAt = now;

    // The earliest of ConsumedAt, SupersededAt and ExpiresAt that has passed; null while the token is usable.
    public DateTimeOffset? UnusableSince(DateTimeOffset now)
    {
        DateTimeOffset? earliest = null;

        foreach (DateTimeOffset? candidate in new DateTimeOffset?[] { ConsumedAt, SupersededAt, ExpiresAt })
        {
            if (candidate is { } value && value <= now && (earliest is null || value < earliest))
            {
                earliest = value;
            }
        }

        return earliest;
    }

    private static EmailToken Create(
        Guid userId,
        EmailTokenPurpose purpose,
        byte[] tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? loginProvider,
        string? providerKey)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        if (tokenHash.Length != TokenHashLength)
        {
            throw new AccountDomainException("Skrót tokenu ma nieprawidłową długość.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new AccountDomainException("Czas ważności tokenu musi być dodatni.");
        }

        return new EmailToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
            LoginProvider = loginProvider,
            ProviderKey = providerKey,
        };
    }
}