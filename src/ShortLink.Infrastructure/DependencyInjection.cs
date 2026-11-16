using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Domain.Interfaces;
using ShortLink.Infrastructure.Cache;
using ShortLink.Infrastructure.Data;
using ShortLink.Infrastructure.Data.Repositories;
using ShortLink.Infrastructure.Services;
using StackExchange.Redis;

namespace ShortLink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. PostgreSQL EF Core DbContext with Connection Pooling and Resilience
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=shortlink_db;Username=postgres;Password=postgres;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;Connection Idle Lifetime=300;";

        services.AddDbContext<ShortLinkDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
                npgsqlOptions.MigrationsAssembly(typeof(ShortLinkDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ShortLinkDbContext>());
        services.AddScoped<IUrlMappingRepository, UrlMappingRepository>();

        // 2. Redis ConnectionMultiplexer (Singleton as recommended by StackExchange.Redis)
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
            try
            {
                var options = ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false; // Allows app to start even if Redis starts after app
                options.ConnectTimeout = 3000;
                options.SyncTimeout = 3000;
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to initialize Redis ConnectionMultiplexer. Falling back to in-memory/direct DB mode.");
                return null!;
            }
        });

        // 3. Cache & Utility Services
        services.AddScoped<IUrlCacheService, RedisCacheService>();
        services.AddSingleton<IBase62Service, Base62Service>();
        services.AddSingleton<IShortCodeGenerator, AutoIncrementShortCodeGenerator>();
        services.AddSingleton<ISnowflakeIdGenerator>(_ => new SnowflakeIdGenerator(machineId: 1));
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
