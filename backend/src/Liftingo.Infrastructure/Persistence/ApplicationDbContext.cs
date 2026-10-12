using System.Text.RegularExpressions;

using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Persistence;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Identity;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liftingo.Infrastructure.Persistence;

public partial class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
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

    // SQL Server error numbers for "duplicate key row" in a unique index (2601) or unique constraint (2627).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            throw new UniqueConstraintViolationException("A unique constraint was violated.", ex)
            {
                IndexName = ExtractIndexName(sql.Message),
            };
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    // "... with unique index 'IX_X'." (2601) or "Violation of UNIQUE KEY constraint 'UQ_X'." (2627)
    private static string? ExtractIndexName(string message)
    {
        Match match = IndexNamePattern().Match(message);

        return match.Success ? match.Groups["name"].Value : null;
    }

    [GeneratedRegex(@"(?:unique index|constraint) '(?<name>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IndexNamePattern();
}
