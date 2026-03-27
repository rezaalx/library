using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minit.Api.Domain;

namespace Minit.Api.Data.EntityConfigurations;

public sealed class UsageAdjustmentConfiguration : IEntityTypeConfiguration<UsageAdjustment>
{
    public void Configure(EntityTypeBuilder<UsageAdjustment> builder)
    {
        builder.ToTable("usage_adjustments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.MonthYYYYMM });

        builder.HasOne(x => x.User)
            .WithMany(u => u.UsageAdjustments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
