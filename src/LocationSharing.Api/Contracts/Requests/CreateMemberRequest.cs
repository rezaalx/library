using System.ComponentModel.DataAnnotations;

namespace LocationSharing.Api.Contracts.Requests;

public class CreateMemberRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }
}
