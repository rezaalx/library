namespace LocationSharing.Api.Contracts.Responses;

public class LocationWriteResponse
{
    public Guid TripPublicId { get; set; }
    public Guid MemberPublicId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }
}
