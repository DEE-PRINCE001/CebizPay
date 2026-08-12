using System.Threading.RateLimiting;
using Asp.Versioning;
using CebizPay.Api.Extensions;
using CebizPay.Api.Middleware;
using CebizPay.Infrastructure;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog Structured Logging
builder.ConfigureSerilog();

// Add Infrastructure & Security
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtSecurity(builder.Configuration);

// Add Observability (OpenTelemetry)
builder.Services.AddObservability(builder.Configuration);

// Add Global Exception Handling & Problem Details
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add API Versioning (/api/v1/...)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Add CORS
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
    ?? new CorsOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment() && corsOptions.AllowedOrigins.Length == 0)
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// Add Rate Limiting Foundation
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("FixedPolicy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Add OpenAPI (.NET 10 approach)
builder.Services.AddOpenApi();

var app = builder.Build();

// Enable Serilog Request Logging
app.UseSerilogRequestLogging();

// Security Headers Middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// Global Exception Handler
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();

app.UseCors("DefaultCorsPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Tags.Count == 0,
    ResponseWriter = HealthCheckExtensions.WriteResponseAsync
});

app.Run();

/// <summary>
/// Program class declaration to support WebApplicationFactory integration testing.
/// </summary>
public partial class Program { }