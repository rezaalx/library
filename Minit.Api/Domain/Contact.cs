namespace Minit.Api.Domain;

public sealed class Contact
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid ContactUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? OwnerUser { get; set; }
    public User? ContactUser { get; set; }
}
