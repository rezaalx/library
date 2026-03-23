namespace LocationSharing.Api.Models;

public class Member
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }

    public ICollection<TripMember> TripMembers { get; set; } = new List<TripMember>();
    public ICollection<MemberLocationHistory> LocationHistory { get; set; } = new List<MemberLocationHistory>();
    public ICollection<MemberLocationLatest> LatestLocations { get; set; } = new List<MemberLocationLatest>();
}
