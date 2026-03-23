namespace LocationSharing.Api.Contracts.Responses;

public class TripMemberResponse
{
    public Guid TripMemberPublicId { get; set; }
    public Guid MemberPublicId { get; set; }
    public Guid TripPublicId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string? MemberDisplayName { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset JoinedOn { get; set; }
}
