using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minit.Api.Domain;

namespace Minit.Api.Data.EntityConfigurations;

public sealed class MonthlyUsageConfiguration : IEntityTypeConfiguration<MonthlyUsage>
{
    public void Configure(EntityTypeBuilder<MonthlyUsage> builder)
    {
        builder.ToTable("monthly_usages");

        builder.HasKey(x => new { x.UserId, x.MonthYYYYMM });

        builder.Property(x => x.UsedSeconds)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.MonthlyUsages)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
