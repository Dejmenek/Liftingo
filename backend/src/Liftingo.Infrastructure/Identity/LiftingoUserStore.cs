using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

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
}
