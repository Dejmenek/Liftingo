using Liftingo.Infrastructure.Audit;

using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

/// <summary>The personal fields of an audit record, decrypted with the user's key.</summary>
public sealed record DecryptedAudit(string IpAddress, string DeviceType);

/// <summary>Replaces the audit sink in tests: remembers every encrypted record.</summary>
public sealed class CapturingAuditSink : IAuditSink
{
    private readonly List<AuditRecord> _records = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<AuditRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return [.. _records];
            }
        }
    }

    public Task WriteAsync(AuditRecord record, CancellationToken ct)
    {
        lock (_lock)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _records.Clear();
        }
    }

    /// <summary>
    /// Decrypts a record with the user's key from the database.
    /// </summary>
    /// <returns><c>null</c> when the record has no key (an event without a user) or the key has been deleted.</returns>
    public static async Task<DecryptedAudit?> DecryptAsync(AuditRecord record, IServiceProvider services)
    {
        if (record.KeyId == Guid.Empty)
        {
            return null;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();

        byte[]? key = await scope.ServiceProvider.GetRequiredService<IAuditKeyProvider>()
            .FindAsync(record.KeyId, CancellationToken.None);

        return key is null
            ? null
            : new DecryptedAudit(
                AuditCipher.Decrypt(key, record.IpAddressCipher),
                AuditCipher.Decrypt(key, record.DeviceTypeCipher));
    }
}