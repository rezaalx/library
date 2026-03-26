using System.ComponentModel.DataAnnotations;

namespace Workspace.Dtos.Contacts;

public sealed class AddContactRequest
{
    [Required]
    public Guid OwnerUserId { get; init; }

    [Required]
    [StringLength(8, MinimumLength = 8)]
    public string ContactCode { get; init; } = string.Empty;
}

public sealed record ContactItemResponse(Guid UserId, string DisplayName, string Code, DateTime AddedAt);
