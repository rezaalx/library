namespace LocationSharing.Api.Contracts.Responses;

public class ValidationErrorResponse
{
    public string Message { get; init; } = "The request is invalid.";
    public IDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}
