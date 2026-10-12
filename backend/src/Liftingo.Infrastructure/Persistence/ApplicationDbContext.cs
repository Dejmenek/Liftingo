using Liftingo.Application.Common.Persistence;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Identity;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liftingo.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext, IDataProtectionKeyContext
{
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

    public DbSet<KnownDevice> KnownDevices => Set<KnownDevice>();

    public DbSet<EmailToken> EmailTokens => Set<EmailToken>();

    // An encryption detail of the audit log, so it is not on IApplicationDbContext.
    public DbSet<AuditEncryptionKey> AuditEncryptionKeys => Set<AuditEncryptionKey>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) =>
        await Database.BeginTransactionAsync(ct);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}