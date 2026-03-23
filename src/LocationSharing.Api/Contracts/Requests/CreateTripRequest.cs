using System.ComponentModel.DataAnnotations;

namespace LocationSharing.Api.Contracts.Requests;

public class CreateTripRequest : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public bool IsActive { get; set; } = true;

    [Range(-90, 90)]
    public double? StartLatitude { get; set; }

    [Range(-180, 180)]
    public double? StartLongitude { get; set; }

    [Range(-90, 90)]
    public double? EndLatitude { get; set; }

    [Range(-180, 180)]
    public double? EndLongitude { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndTime <= StartTime)
        {
            yield return new ValidationResult("EndTime must be greater than StartTime.", [nameof(EndTime)]);
        }
    }
}
