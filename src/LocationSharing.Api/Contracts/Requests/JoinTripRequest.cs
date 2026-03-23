using System.ComponentModel.DataAnnotations;
using LocationSharing.Api.Contracts.Validation;

namespace LocationSharing.Api.Contracts.Requests;

public class JoinTripRequest
{
    [NotEmptyGuid]
    public Guid MemberPublicId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Code { get; set; } = string.Empty;
}
