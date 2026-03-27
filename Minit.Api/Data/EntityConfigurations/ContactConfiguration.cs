using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minit.Api.Domain;

namespace Minit.Api.Data.EntityConfigurations;

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(x => x.OwnerUser)
            .WithMany(x => x.OwnedContacts)
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ContactUser)
            .WithMany(x => x.ContactOfUsers)
            .HasForeignKey(x => x.ContactUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OwnerUserId, x.ContactUserId })
            .IsUnique();
    }
}
