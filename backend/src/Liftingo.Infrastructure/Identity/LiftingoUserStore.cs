using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    public override Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // UserManager calls UpdateAsync after most changes (AddLoginAsync, ...). The base implementation calls
        // DbContext.Update, which would turn a user that was staged earlier in the same unit of work from Added
        // into Modified, and the INSERT would become an UPDATE of a row that does not exist.
        if (Context.Entry(user).State == EntityState.Added)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return base.UpdateAsync(user, cancellationToken);
    }
}
