using System.ComponentModel.DataAnnotations;

namespace LocationSharing.Api.Contracts.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is Guid guid && guid != Guid.Empty)
        {
            return ValidationResult.Success;
        }

        var message = ErrorMessage ?? $"{validationContext.MemberName} must be a non-empty GUID.";
        return new ValidationResult(message);
    }
}
