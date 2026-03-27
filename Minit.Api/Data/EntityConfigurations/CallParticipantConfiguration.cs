using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minit.Api.Domain;

namespace Minit.Api.Data.EntityConfigurations;

public sealed class CallParticipantConfiguration : IEntityTypeConfiguration<CallParticipant>
{
    public void Configure(EntityTypeBuilder<CallParticipant> builder)
    {
        builder.ToTable("call_participants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BilledSeconds)
            .IsRequired();

        builder.Property(x => x.JoinedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.LeftAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.CallSessionId, x.UserId })
            .IsUnique();

        builder.HasOne(x => x.CallSession)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.CallSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.CallParticipants)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
