using LocationSharing.Api.Contracts.Responses;
using LocationSharing.Api.Data;
using LocationSharing.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kvp => string.IsNullOrWhiteSpace(kvp.Key) ? "request" : kvp.Key,
                kvp => kvp.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The value is invalid."
                        : error.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(new ValidationErrorResponse
        {
            Message = "The request is invalid.",
            Errors = errors
        });
    };
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<LocationSharingDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new
{
    service = "LocationSharing API",
    status = "ok",
    utcNow = DateTimeOffset.UtcNow
}));

app.MapControllers();

app.Run();
