using System.ComponentModel.DataAnnotations;

namespace Minit.Api.Dtos;

public sealed record AddContactRequest(
    [property: Required] Guid OwnerUserId,
    [property: Required, StringLength(8, MinimumLength = 8)] string ContactCode);

public sealed record AddContactResponse(
    Guid ContactId,
    Guid OwnerUserId,
    Guid ContactUserId,
    string DisplayName,
    DateTime CreatedAtUtc);

public sealed record ContactItemResponse(
    Guid ContactUserId,
    string DisplayName,
    string Code,
    DateTime AddedAtUtc);
