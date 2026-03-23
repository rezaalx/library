namespace LocationSharing.Api.Models;

public class TripMember
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int MemberId { get; set; }
    public int TripId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset JoinedOn { get; set; }

    public Member Member { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
}
