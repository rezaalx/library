namespace LocationSharing.Api.Models;

public class MemberLocationHistory
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int MemberId { get; set; }
    public int TripId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset CreatedOn { get; set; }

    public Member Member { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
}
