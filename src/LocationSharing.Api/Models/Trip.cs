namespace LocationSharing.Api.Models;

public class Trip
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public bool IsActive { get; set; }
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public double? EndLatitude { get; set; }
    public double? EndLongitude { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }

    public ICollection<TripMember> TripMembers { get; set; } = new List<TripMember>();
    public ICollection<MemberLocationHistory> LocationHistory { get; set; } = new List<MemberLocationHistory>();
    public ICollection<MemberLocationLatest> LatestLocations { get; set; } = new List<MemberLocationLatest>();
}
