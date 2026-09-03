using System.Threading.RateLimiting;
using Asp.Versioning;
using CebizPay.Api.Extensions;
using CebizPay.Api.Middleware;
using CebizPay.Application;
using CebizPay.Infrastructure;
using CebizPay.Infrastructure.Common;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

// Load local .env configuration if present
EnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog Structured Logging
builder.ConfigureSerilog();

// Configure Forwarded Headers for reverse proxy / container deployments
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure HSTS for production deployments
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Add Application Services (MediatR & Validators)
builder.Services.AddApplication();

// Add Infrastructure & Security
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtSecurity(builder.Configuration);

// Add Observability (OpenTelemetry)
builder.Services.AddObservability(builder.Configuration);

// Add Global Exception Handling & Problem Details
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add Controllers
builder.Services.AddControllers();

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

var defaultDevOrigins = new[]
{
    "http://localhost:5173",
    "http://127.0.0.1:5173",
    "http://localhost:5015",
    "http://127.0.0.1:5015",
    "http://localhost:3000",
    "http://127.0.0.1:3000"
};

var effectiveOrigins = corsOptions.AllowedOrigins.Length > 0
    ? corsOptions.AllowedOrigins
    : (builder.Environment.IsDevelopment() ? defaultDevOrigins : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        if (effectiveOrigins.Length > 0)
        {
            policy.WithOrigins(effectiveOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            // Fail closed in production when no CORS origins are configured
            policy.SetIsOriginAllowed(_ => false);
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
    options.AddFixedWindowLimiter("AuthLoginPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("OtpRequestPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("OtpVerificationPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("MfaVerificationPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("FinancialTransferPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
    options.AddFixedWindowLimiter("PinVerificationPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Add OpenAPI services with Bearer token support
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "CebizPay API",
            Version = "v1"
        };

        var securityScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Enter JWT Bearer token only (do NOT include 'Bearer ' prefix). Example: eyJhbGciOi..."
        };

        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = securityScheme;

        document.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
        var securityRequirement = new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document),
                new List<string>()
            }
        };

        document.Security.Add(securityRequirement);
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var hasAuthorize = metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any();
        var isAllowAnonymous = metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();

        if (hasAuthorize && !isAllowAnonymous)
        {
            operation.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
            operation.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", context.Document),
                    new List<string>()
                }
            });
        }

        return Task.CompletedTask;
    });
});


var app = builder.Build();

// Forwarded Headers (must run first so downstream middlewares observe forwarded scheme and IP)
app.UseForwardedHeaders();

// Correlation ID Tracking
app.UseMiddleware<CorrelationIdMiddleware>();

// Enable Serilog Request Logging
app.UseSerilogRequestLogging();

// Security Headers Middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/health"), branch =>
    {
        branch.UseHttpsRedirection();
    });
}

// Global Exception Handler
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var superAdminOptions = scope.ServiceProvider.GetRequiredService<IOptions<SuperAdminSeedOptions>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await SuperAdminSeeder.SeedSuperAdminAsync(userManager, dbContext, superAdminOptions, logger);
    }

    app.MapOpenApi();

    app.UseSwaggerUI(c =>
    {
        c.DocumentTitle = "CebizPay API Documentation";
        c.RoutePrefix = "swagger";
        c.SwaggerEndpoint("/openapi/v1.json", "CebizPay API v1");
        c.ConfigObject.AdditionalItems["persistAuthorization"] = true;
    });
}

app.UseRouting();

app.UseCors("DefaultCorsPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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
