using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liftingo.Infrastructure.Persistence.Configurations;

internal sealed class EmailTokenConfiguration : IEntityTypeConfiguration<EmailToken>
{
    public void Configure(EntityTypeBuilder<EmailToken> builder)
    {
        builder.ToTable(
            "EmailTokens",
            table => table.HasCheckConstraint(
                "CK_EmailTokens_ProviderFields",
                "([Purpose] = 'ProviderLinkConfirmation' AND [LoginProvider] IS NOT NULL AND [ProviderKey] IS NOT NULL) " +
                "OR ([Purpose] <> 'ProviderLinkConfirmation' AND [LoginProvider] IS NULL AND [ProviderKey] IS NULL)"));

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .HasColumnType("binary(32)")
            .IsRequired();

        builder.Property(t => t.LoginProvider).HasMaxLength(EmailToken.LoginProviderMaxLength);
        builder.Property(t => t.ProviderKey).HasMaxLength(EmailToken.ProviderKeyMaxLength);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.Purpose, t.CreatedAt });
        builder.HasIndex(t => t.ExpiresAt);
    }
}