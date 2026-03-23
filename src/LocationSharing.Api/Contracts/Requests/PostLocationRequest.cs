using System.ComponentModel.DataAnnotations;
using LocationSharing.Api.Contracts.Validation;

namespace LocationSharing.Api.Contracts.Requests;

public class PostLocationRequest : IValidatableObject
{
    [NotEmptyGuid]
    public Guid MemberPublicId { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    public double? Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTimeOffset RecordedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RecordedAt == default)
        {
            yield return new ValidationResult("RecordedAt is required.", [nameof(RecordedAt)]);
        }
    }
}
