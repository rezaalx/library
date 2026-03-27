using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minit.Api.Data;
using Minit.Api.Domain;
using Minit.Api.Dtos;
using Minit.Api.Services;

namespace Minit.Api.Endpoints;

public static class UsersEndpoints
{
    public static RouteGroupBuilder MapUsersEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/users").WithTags("Users");

        group.MapPost("/register", RegisterUserAsync)
            .WithName("RegisterUser")
            .WithSummary("Register user and generate unique code")
            .RequireRateLimiting("public-per-ip")
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/by-code/{code}", GetByCodeAsync)
            .WithName("GetUserByCode")
            .WithSummary("Find user by 8-char code")
            .RequireRateLimiting("public-per-ip")
            .Produces<UserByCodeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static async Task<IResult> RegisterUserAsync(
        [FromBody] RegisterUserRequest request,
        AppDbContext dbContext,
        IUserCodeGenerator userCodeGenerator,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName.Trim();
        var now = DateTimeProvider.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            MonthlyLimitSeconds = 6000,
            IsActive = true,
            CreatedAt = now
        };

        const int maxRetries = 8;
        for (var i = 0; i < maxRetries; i++)
        {
            user.Code = userCodeGenerator.Generate();
            dbContext.Users.Add(user);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return Results.Created(
                    $"/api/users/{user.Id}",
                    new RegisterUserResponse(user.Id, user.DisplayName, user.Code, user.MonthlyLimitSeconds, user.CreatedAt));
            }
            catch (DbUpdateException) when (i < maxRetries - 1)
            {
                dbContext.Entry(user).State = EntityState.Detached;
            }
        }

        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Could not allocate unique user code",
            detail: "Failed to generate a unique user code after multiple attempts.",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "code_generation_conflict" });
    }

    private static async Task<IResult> GetByCodeAsync(
        string code,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid code",
                detail: "Code must be exactly 8 characters.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_code" });
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsActive && x.Code == normalizedCode)
            .Select(x => new UserByCodeResponse(x.Id, x.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: "No active user found for the provided code.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "user_not_found" })
            : Results.Ok(user);
    }
}
