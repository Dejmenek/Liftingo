using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// The EF Core user store with auto-save turned off, so <c>UserManager</c> only stages changes and the
/// slice handler commits everything with one <c>SaveChangesAsync</c>.
/// </summary>
public sealed class LiftingoUserStore : UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>
{
    public LiftingoUserStore(ApplicationDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        AutoSaveChanges = false;
    }

    public override async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // UserManager calls UpdateAsync after most changes (AddLoginAsync, ...). The base implementation calls
        // DbContext.Update, which would turn a user that was staged earlier in the same unit of work from Added
        // into Modified, and the INSERT would become an UPDATE of a row that does not exist.
        if (Context.Entry(user).State == EntityState.Added)
        {
            return IdentityResult.Success;
        }

        // Without a save in between, UserManager can call UpdateAsync several times for one user (a password reset
        // changes the hash, then the lockout). Each call sets a new ConcurrencyStamp, and on the later calls EF takes
        // the previous stamp as the "original" one, so the final UPDATE would look for a stamp that is not in the
        // database and throw DbUpdateConcurrencyException. Keep the stamp that was loaded as the original.
        PropertyEntry<ApplicationUser, string?> stamp = Context.Entry(user).Property(u => u.ConcurrencyStamp);
        string? loadedStamp = stamp.OriginalValue;

        IdentityResult result = await base.UpdateAsync(user, cancellationToken);

        stamp.OriginalValue = loadedStamp;

        return result;
    }
}
