using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workspace.Data;
using Workspace.Endpoints;
using Workspace.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidation();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        return;
    }

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<QuotaService>();
builder.Services.AddSingleton<ICodeGenerator, CodeGenerator>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static (context, _) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var task = context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Rate limit exceeded for this endpoint.",
            Type = "https://httpstatuses.com/429"
        });

        return new ValueTask(task);
    };

    options.AddPolicy("public-per-ip", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode < 400 || response.HasStarted)
    {
        return;
    }

    response.ContentType = "application/problem+json";
    await response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = response.StatusCode,
        Title = "Request failed",
        Detail = $"Request failed with status code {response.StatusCode}.",
        Type = $"https://httpstatuses.com/{response.StatusCode}"
    });
});
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new { service = "call-usage-api", utcNow = DateTime.UtcNow }));

var api = app.MapGroup("/api");
api.MapUserEndpoints();
api.MapContactEndpoints();
api.MapCallEndpoints();
api.MapUsageEndpoints();
api.MapAdminEndpoints();

app.Run();

public partial class Program;
