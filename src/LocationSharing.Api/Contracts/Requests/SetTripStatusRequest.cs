using System.ComponentModel.DataAnnotations;

namespace LocationSharing.Api.Contracts.Requests;

public class SetTripStatusRequest
{
    [Required]
    public bool? IsActive { get; set; }
}
