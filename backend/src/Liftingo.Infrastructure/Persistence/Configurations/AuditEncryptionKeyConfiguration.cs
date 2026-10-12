using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liftingo.Infrastructure.Persistence.Configurations;

internal sealed class AuditEncryptionKeyConfiguration : IEntityTypeConfiguration<AuditEncryptionKey>
{
    public void Configure(EntityTypeBuilder<AuditEncryptionKey> builder)
    {
        builder.ToTable("AuditEncryptionKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.ProtectedKey)
            .HasColumnType("varbinary(max)")
            .IsRequired();

        // Cascade delete is the crypto-shredding path: removing the account removes the key.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(k => k.UserId).IsUnique();
    }
}