using Microsoft.AspNetCore.Mvc;

namespace LocationSharing.Api.Utilities;

public static class ControllerProblemExtensions
{
    public static ObjectResult ProblemWithTrace(
        this ControllerBase controller,
        int statusCode,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = controller.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;
        return controller.StatusCode(statusCode, problem);
    }
}
