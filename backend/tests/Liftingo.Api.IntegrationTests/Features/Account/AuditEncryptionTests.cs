using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Infrastructure.Audit;

using Microsoft.EntityFrameworkCore;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class AuditEncryptionTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ClientIp = "203.0.113.20";
    private const string Password = "Sztanga1!";

    [Fact]
    public async Task DeleteUser_AfterAuditedEvents_RemovesTheKeyRowAndTheRecordsCanNoLongerBeDecrypted()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, var request) = await client.RegisterAsync(password: Password);
        using HttpResponseMessage owned = registered;
        Guid userId = (await AuthTestClient.ReadSessionAsync(registered)).Account.Id;
        (await client.LoginAsync(request.Email, Password, clientIp: ClientIp)).Dispose();
        (await client.LoginAsync(request.Email, "Zle-haslo1!", clientIp: ClientIp)).Dispose();

        List<AuditRecord> records = [.. Factory.Audit.Records];
        Assert.Equal(2, records.Count);
        Guid keyId = Assert.Single(records.Select(r => r.KeyId).Distinct());
        Assert.NotEqual(Guid.Empty, keyId);

        // Before the deletion the records are readable with the user's key.
        foreach (AuditRecord record in records)
        {
            DecryptedAudit? decrypted = await CapturingAuditSink.DecryptAsync(record, Factory.Services);
            Assert.NotNull(decrypted);
            Assert.Equal(ClientIp, decrypted.IpAddress);
        }

        Assert.Equal(1, await WithDbAsync(db => db.AuditEncryptionKeys
            .CountAsync(k => k.UserId == userId, TestContext.Current.CancellationToken)));

        // The account is deleted; the key goes with it through the foreign key cascade.
        int deleted = await WithDbAsync(db => db.Users
            .Where(u => u.Id == userId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, deleted);

        Assert.Equal(0, await WithDbAsync(db => db.AuditEncryptionKeys
            .CountAsync(k => k.Id == keyId, TestContext.Current.CancellationToken)));

        foreach (AuditRecord record in records)
        {
            Assert.Null(await CapturingAuditSink.DecryptAsync(record, Factory.Services));
        }
    }

    [Fact]
    public async Task DeleteUser_OtherUsersKey_StaysAndTheirRecordsStayReadable()
    {
        using AuthTestClient first = Factory.CreateAuthClient();
        using AuthTestClient second = Factory.CreateAuthClient();
        (HttpResponseMessage firstRegistered, var firstRequest) = await first.RegisterAsync(password: Password);
        using HttpResponseMessage firstOwned = firstRegistered;
        (HttpResponseMessage secondRegistered, var secondRequest) = await second.RegisterAsync(password: Password);
        using HttpResponseMessage secondOwned = secondRegistered;
        Guid firstId = (await AuthTestClient.ReadSessionAsync(firstRegistered)).Account.Id;
        Guid secondId = (await AuthTestClient.ReadSessionAsync(secondRegistered)).Account.Id;
        (await first.LoginAsync(firstRequest.Email, Password, clientIp: ClientIp)).Dispose();
        (await second.LoginAsync(secondRequest.Email, Password, clientIp: ClientIp)).Dispose();

        await WithDbAsync(db => db.Users.Where(u => u.Id == firstId).ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        AuditRecord firstRecord = Assert.Single(Factory.Audit.Records, r => r.UserId == firstId);
        AuditRecord secondRecord = Assert.Single(Factory.Audit.Records, r => r.UserId == secondId);
        Assert.Null(await CapturingAuditSink.DecryptAsync(firstRecord, Factory.Services));
        DecryptedAudit? remaining = await CapturingAuditSink.DecryptAsync(secondRecord, Factory.Services);
        Assert.NotNull(remaining);
        Assert.Equal(ClientIp, remaining.IpAddress);
    }
}
