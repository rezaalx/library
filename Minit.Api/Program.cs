using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Minit.Api.Data;
using Minit.Api.Endpoints;
using Minit.Api.Security;
using Minit.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.Configure<RouteHandlerOptions>(options =>
{
    options.ThrowOnBadRequest = true;
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WhatsApp-Like Backend API",
        Version = "v1",
        Description = "Backend API for user registration, contacts, calling, and quota management."
    });
    options.AddSecurityDefinition("AdminApiKey", new OpenApiSecurityScheme
    {
        Name = AdminApiKeyConstants.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Admin API key header for /api/admin endpoints."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "AdminApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<IUserCodeGenerator, UserCodeGenerator>();
builder.Services.AddScoped<AdminApiKeyEndpointFilter>();
builder.Services.AddScoped<ValidationEndpointFilter>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-per-ip", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        var problem = new
        {
            type = "https://httpstatuses.com/429",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Rate limit exceeded. Try again later.",
            errorCode = "rate_limited",
            traceId = context.HttpContext.TraceIdentifier
        };
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api")
    .AddEndpointFilter<ValidationEndpointFilter>();

api.MapUsersEndpoints();
api.MapContactsEndpoints();
api.MapCallsEndpoints();
api.MapUsageEndpoints();
api.MapAdminEndpoints();

app.Run();

public partial class Program;
