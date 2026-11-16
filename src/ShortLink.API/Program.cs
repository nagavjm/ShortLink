using Serilog;
using ShortLink.API.Middleware;
using ShortLink.Application;
using ShortLink.Infrastructure;
using ShortLink.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 5. Middleware Pipeline
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

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors("AllowAngularClient");

app.UseAuthorization();

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
