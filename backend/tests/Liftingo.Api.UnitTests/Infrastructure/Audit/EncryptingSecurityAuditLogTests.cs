using Liftingo.Application.Common.Security;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace Liftingo.Api.UnitTests.Infrastructure.Audit;

public class EncryptingSecurityAuditLogTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeKeyProvider _keys = new();
    private readonly RecordingSink _sink = new();
    private readonly EncryptingSecurityAuditLog _log;

    public EncryptingSecurityAuditLogTests() => _log = new EncryptingSecurityAuditLog(_keys, _sink);

    [Fact]
    public async Task WriteAsync_EventWithUser_EncryptsIpAndDeviceUnderTheUsersKey()
    {
        var userId = Guid.NewGuid();

        await _log.WriteAsync(Event(userId, "203.0.113.7", "Chrome, Windows, komputer"), TestContext.Current.CancellationToken);

        AuditRecord record = Assert.Single(_sink.Records);
        byte[] key = _keys.KeyOf(userId);
        Assert.Equal(userId, record.UserId);
        Assert.Equal(_keys.KeyIdOf(userId), record.KeyId);
        Assert.Equal(SecurityEventType.Login, record.EventType);
        Assert.Equal(SecurityEventOutcome.Failed, record.Outcome);
        Assert.Equal(s_now, record.OccurredAt);
        Assert.Equal("203.0.113.7", AuditCipher.Decrypt(key, record.IpAddressCipher));
        Assert.Equal("Chrome, Windows, komputer", AuditCipher.Decrypt(key, record.DeviceTypeCipher));
        Assert.DoesNotContain("203.0.113.7", record.IpAddressCipher, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_SameValueTwice_UsesAFreshNonceEachTime()
    {
        var userId = Guid.NewGuid();

        await _log.WriteAsync(Event(userId, "203.0.113.7", "same"), TestContext.Current.CancellationToken);
        await _log.WriteAsync(Event(userId, "203.0.113.7", "same"), TestContext.Current.CancellationToken);

        AuditRecord first = _sink.Records[0];
        AuditRecord second = _sink.Records[1];
        Assert.NotEqual(first.IpAddressCipher, second.IpAddressCipher);
        Assert.NotEqual(Nonce(first.IpAddressCipher), Nonce(second.IpAddressCipher));
    }

    [Fact]
    public async Task WriteAsync_IpAndDeviceWithEqualText_UseDifferentNonces()
    {
        await _log.WriteAsync(Event(Guid.NewGuid(), "same", "same"), TestContext.Current.CancellationToken);

        AuditRecord record = Assert.Single(_sink.Records);
        Assert.NotEqual(Nonce(record.IpAddressCipher), Nonce(record.DeviceTypeCipher));
    }

    [Fact]
    public async Task WriteAsync_DecryptingWithAnotherUsersKey_Fails()
    {
        var userId = Guid.NewGuid();
        await _log.WriteAsync(Event(userId, "203.0.113.7", "device"), TestContext.Current.CancellationToken);
        byte[] otherKey = _keys.KeyOf(Guid.NewGuid());

        AuditRecord record = Assert.Single(_sink.Records);

        Assert.ThrowsAny<CryptographicException>(() => AuditCipher.Decrypt(otherKey, record.IpAddressCipher));
    }

    [Fact]
    public async Task WriteAsync_EventWithoutUser_HasEmptyKeyIdAndCiphersAndCreatesNoKey()
    {
        var auditEvent = new SecurityAuditEvent(
            null, SecurityEventType.Login, SecurityEventOutcome.Failed, s_now, IpAddress: null, DeviceType: null);

        await _log.WriteAsync(auditEvent, TestContext.Current.CancellationToken);

        AuditRecord record = Assert.Single(_sink.Records);
        Assert.Null(record.UserId);
        Assert.Equal(Guid.Empty, record.KeyId);
        Assert.Equal(string.Empty, record.IpAddressCipher);
        Assert.Equal(string.Empty, record.DeviceTypeCipher);
        Assert.Equal(0, _keys.CreatedCount);
    }

    [Fact]
    public async Task WriteAsync_TwoEventsOfOneUser_ShareOneKey()
    {
        var userId = Guid.NewGuid();

        await _log.WriteAsync(Event(userId, "a", "b"), TestContext.Current.CancellationToken);
        await _log.WriteAsync(Event(userId, "c", "d"), TestContext.Current.CancellationToken);

        Assert.Equal(1, _keys.CreatedCount);
        Assert.Equal(_sink.Records[0].KeyId, _sink.Records[1].KeyId);
    }

    private static SecurityAuditEvent Event(Guid userId, string ip, string device) =>
        new(userId, SecurityEventType.Login, SecurityEventOutcome.Failed, s_now, ip, device);

    // The first 12 bytes of nonce + ciphertext + tag.
    private static string Nonce(string cipher) => Convert.ToHexString(Convert.FromBase64String(cipher).AsSpan(0, 12));

    private sealed class RecordingSink : IAuditSink
    {
        public List<AuditRecord> Records { get; } = [];

        public Task WriteAsync(AuditRecord record, CancellationToken ct)
        {
            Records.Add(record);

            return Task.CompletedTask;
        }
    }

    // Keeps keys wrapped with the ephemeral Data Protection provider, like the real provider keeps them
    // wrapped in the database.
    private sealed class FakeKeyProvider : IAuditKeyProvider
    {
        private readonly IDataProtector _protector =
            new EphemeralDataProtectionProvider().CreateProtector(AuditKeyProvider.Purpose);

        private readonly Dictionary<Guid, (Guid Id, byte[] Protected)> _byUser = [];

        public int CreatedCount => _byUser.Count;

        public Task<AuditKey> GetOrCreateAsync(Guid userId, CancellationToken ct)
        {
            if (!_byUser.TryGetValue(userId, out (Guid Id, byte[] Protected) entry))
            {
                entry = (Guid.NewGuid(), _protector.Protect(RandomNumberGenerator.GetBytes(32)));
                _byUser[userId] = entry;
            }

            return Task.FromResult(new AuditKey(entry.Id, _protector.Unprotect(entry.Protected)));
        }

        public Task<byte[]?> FindAsync(Guid keyId, CancellationToken ct)
        {
            foreach ((Guid id, byte[] wrapped) in _byUser.Values)
            {
                if (id == keyId)
                {
                    return Task.FromResult<byte[]?>(_protector.Unprotect(wrapped));
                }
            }

            return Task.FromResult<byte[]?>(null);
        }

        // For tests that need the key of a user without going through WriteAsync (creates one on demand).
        public byte[] KeyOf(Guid userId) => GetOrCreateAsync(userId, CancellationToken.None).Result.Key;

        public Guid KeyIdOf(Guid userId) => GetOrCreateAsync(userId, CancellationToken.None).Result.Id;
    }
}