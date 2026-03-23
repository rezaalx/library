using LocationSharing.Api.Contracts.Validation;

namespace LocationSharing.Api.Contracts.Requests;

public class LeaveTripRequest
{
    [NotEmptyGuid]
    public Guid MemberPublicId { get; set; }
}
