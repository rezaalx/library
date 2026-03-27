using System.ComponentModel.DataAnnotations;

namespace Minit.Api.Services;

public sealed class ValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validationFailures = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in context.Arguments.Where(x => x is not null))
        {
            var validationContext = new ValidationContext(argument!);
            var validationResults = new List<ValidationResult>();

            if (Validator.TryValidateObject(argument!, validationContext, validationResults, validateAllProperties: true))
            {
                continue;
            }

            foreach (var validationResult in validationResults)
            {
                var memberName = validationResult.MemberNames.FirstOrDefault() ?? string.Empty;
                if (!validationFailures.TryGetValue(memberName, out var errors))
                {
                    validationFailures[memberName] = [validationResult.ErrorMessage ?? "Validation failed."];
                }
                else
                {
                    validationFailures[memberName] = [.. errors, validationResult.ErrorMessage ?? "Validation failed."];
                }
            }
        }

        if (validationFailures.Count > 0)
        {
            return Results.ValidationProblem(validationFailures);
        }

        return await next(context);
    }
}
