using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minit.Api.Domain;

namespace Minit.Api.Data.EntityConfigurations;

public sealed class CallSessionConfiguration : IEntityTypeConfiguration<CallSession>
{
    public void Configure(EntityTypeBuilder<CallSession> builder)
    {
        builder.ToTable("call_sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProviderRoomId).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EndedAt).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => x.Status);

        builder
            .HasOne(x => x.CreatedByUser)
            .WithMany(u => u.CreatedCallSessions)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
