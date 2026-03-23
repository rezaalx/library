using System.ComponentModel.DataAnnotations;
using LocationSharing.Api.Contracts.Validation;

namespace LocationSharing.Api.Contracts.Requests;

public class JoinTripRequest : IValidatableObject
{
    [NotEmptyGuid]
    public Guid MemberPublicId { get; set; }

    public Guid? TripPublicId { get; set; }

    [MaxLength(16)]
    public string? Code { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TripPublicId.HasValue && TripPublicId.Value == Guid.Empty)
        {
            yield return new ValidationResult(
                "TripPublicId must be a non-empty GUID when provided.",
                [nameof(TripPublicId)]);
        }

        if (!TripPublicId.HasValue && string.IsNullOrWhiteSpace(Code))
        {
            yield return new ValidationResult(
                "Either tripPublicId or code must be provided.",
                [nameof(TripPublicId), nameof(Code)]);
        }
    }
}
