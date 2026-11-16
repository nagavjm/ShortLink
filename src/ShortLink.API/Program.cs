using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using ShortLink.API.Middleware;
using ShortLink.Application;
using ShortLink.Infrastructure;
using ShortLink.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog structured logging with context enrichment
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ShortLink.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Register Clean Architecture Layer Services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. Register Controllers and JSON serialization
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ShortLink API - Scalable URL Shortener",
        Version = "v1",
        Description = "Production-oriented Bitly-like URL Shortener built with ASP.NET Core (.NET 8), PostgreSQL, and Redis."
    });
});

// 4. Configure CORS for Angular Frontend
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 5. Configure ASP.NET Core Built-in Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";

        var retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = Math.Max(1, (int)retryAfter.TotalSeconds);
        }

        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = $"Rate limit exceeded. Maximum 100 URL creations per minute per client IP. Please try again after {retryAfterSeconds} seconds.",
            Instance = context.HttpContext.Request.Path
        };

        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: token);
    };

    // Partitioned rate limiter policy for URL creation: 100 requests per minute per client IP
    options.AddPolicy("UrlCreationPolicy", httpContext =>
    {
        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown_client_ip";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0 // Reject immediately with 429 when capacity is exceeded
            });
    });
});

var app = builder.Build();

// 6. Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "0");
    await next();
});

// 7. Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShortLink API v1");
        c.RoutePrefix = "swagger";
    });
}

// Serilog structured HTTP request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

app.UseCors("AllowAngularClient");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Rate limiting
app.UseRateLimiter();

app.UseAuthorization();

// 8. Health Check Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Liveness probe checks process availability only
});

app.MapControllers();

try
{
    Log.Information("Starting ShortLink Web API application...");
    await DatabaseInitializer.MigrateDatabaseAsync(app.Services);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ShortLink Web API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Formats health check report into structured RFC-style JSON
static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        Status = report.Status.ToString(),
        TotalDurationMs = report.TotalDuration.TotalMilliseconds,
        Timestamp = DateTime.UtcNow,
        Checks = report.Entries.Select(e => new
        {
            Component = e.Key,
            Status = e.Value.Status.ToString(),
            DurationMs = e.Value.Duration.TotalMilliseconds,
            Description = e.Value.Description,
            Data = e.Value.Data
        })
    };

    return context.Response.WriteAsync(
        JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
}
