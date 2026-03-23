namespace LocationSharing.Api.Contracts.Responses;

public class LocationLatestResponse
{
    public Guid MemberPublicId { get; set; }
    public Guid TripPublicId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }
}
