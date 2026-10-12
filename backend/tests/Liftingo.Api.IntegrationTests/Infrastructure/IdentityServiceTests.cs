using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Infrastructure.Identity;
using Liftingo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

// Confirms research R2's "verify" item: with AutoSaveChanges = false, UserManager only stages changes.
public class IdentityServiceTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Email = "anna@example.pl";
    private const string ValidPassword = "Sztanga1!";

    [Fact]
    public async Task CreateUserAsync_ValidUser_RunsValidatorsAndStagesTheUserWithoutSaving()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Result<Guid> result = await identity.CreateUserAsync(Email, ValidPassword, emailConfirmed: false, Factory.Time.GetUtcNow(), ct);

        Assert.True(result.IsSuccess);
        ApplicationUser staged = Assert.Single(db.ChangeTracker.Entries<ApplicationUser>()).Entity;
        Assert.Equal(result.Value, staged.Id);
        Assert.Equal(EntityState.Added, db.Entry(staged).State);
        Assert.NotNull(staged.PasswordHash);
        Assert.Equal("ANNA@EXAMPLE.PL", staged.NormalizedEmail);
        Assert.Equal(Factory.Time.GetUtcNow(), staged.CreatedAt);
        Assert.Equal(0, await CountUsersAsync(ct));

        await db.SaveChangesAsync(ct);

        Assert.Equal(1, await CountUsersAsync(ct));
    }

    [Fact]
    public async Task CreateUserAsync_PasswordBreaksPolicy_ReturnsValidationErrorAndStagesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Result<Guid> result = await identity.CreateUserAsync(Email, "abcdefgh", emailConfirmed: false, Factory.Time.GetUtcNow(), ct);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(error.Errors, e => e.Code == "Validation.PasswordRequiresDigit");
        Assert.Contains(error.Errors, e => e.Code == "Validation.PasswordRequiresSpecial");
        Assert.Empty(db.ChangeTracker.Entries<ApplicationUser>());
        Assert.Equal(0, await CountUsersAsync(ct));
    }

    [Fact]
    public async Task CreateUserAsync_FollowedByLoginAndConfirmation_InsertsEverythingWithOneSave()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Result<Guid> created = await identity.CreateUserAsync(Email, ValidPassword, emailConfirmed: false, Factory.Time.GetUtcNow(), ct);

        // UserManager calls UpdateAsync after both; that must not turn the staged INSERT into an UPDATE.
        await identity.AddLoginAsync(created.Value, "Google", "google-key-1", "Google", ct);
        await identity.ConfirmEmailAsync(created.Value, ct);

        Assert.Equal(0, await CountUsersAsync(ct));

        await db.SaveChangesAsync(ct);

        await using AsyncServiceScope other = Factory.Services.CreateAsyncScope();
        AccountSummary? summary = await other.ServiceProvider.GetRequiredService<IIdentityService>()
            .GetSummaryAsync(created.Value, ct);
        Assert.NotNull(summary);
        Assert.True(summary.EmailVerified);
        Assert.Equal(["Google"], summary.Providers);
    }

    private async Task<int> CountUsersAsync(CancellationToken ct) =>
        await WithDbAsync(db => db.Users.CountAsync(ct));
}
