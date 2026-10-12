using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liftingo.Infrastructure.Persistence.Configurations;

internal sealed class KnownDeviceConfiguration : IEntityTypeConfiguration<KnownDevice>
{
    public void Configure(EntityTypeBuilder<KnownDevice> builder)
    {
        builder.ToTable("KnownDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceType)
            .HasMaxLength(KnownDevice.DeviceTypeMaxLength)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();
        builder.HasIndex(d => d.LastSeenAt);
    }
}