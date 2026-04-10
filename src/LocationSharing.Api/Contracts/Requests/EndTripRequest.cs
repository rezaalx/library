using System.ComponentModel.DataAnnotations;

namespace LocationSharing.Api.Contracts.Requests;

public class EndTripRequest : IValidatableObject
{
    [Range(-90, 90)]
    public double? EndLatitude { get; set; }

    [Range(-180, 180)]
    public double? EndLongitude { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndLatitude.HasValue != EndLongitude.HasValue)
        {
            yield return new ValidationResult(
                "EndLatitude and EndLongitude must both be provided when setting an end location.",
                [nameof(EndLatitude), nameof(EndLongitude)]);
        }

        if (EndLatitude.HasValue && double.IsNaN(EndLatitude.Value))
        {
            yield return new ValidationResult(
                "EndLatitude must be a valid number.",
                [nameof(EndLatitude)]);
        }

        if (EndLatitude.HasValue && double.IsInfinity(EndLatitude.Value))
        {
            yield return new ValidationResult(
                "EndLatitude must be a finite number.",
                [nameof(EndLatitude)]);
        }

        if (EndLongitude.HasValue && double.IsNaN(EndLongitude.Value))
        {
            yield return new ValidationResult(
                "EndLongitude must be a valid number.",
                [nameof(EndLongitude)]);
        }

        if (EndLongitude.HasValue && double.IsInfinity(EndLongitude.Value))
        {
            yield return new ValidationResult(
                "EndLongitude must be a finite number.",
                [nameof(EndLongitude)]);
        }
    }
}
