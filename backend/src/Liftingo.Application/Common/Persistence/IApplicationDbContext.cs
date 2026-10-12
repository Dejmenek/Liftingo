using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liftingo.Application.Common.Persistence;

public interface IApplicationDbContext
{
    DbSet<UserConsent> UserConsents { get; }

    DbSet<KnownDevice> KnownDevices { get; }

    DbSet<EmailToken> EmailTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // For handlers that combine ExecuteDeleteAsync with SaveChangesAsync and need both to commit together.
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
}