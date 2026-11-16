using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ShortLink.Infrastructure.Data;

/// <summary>
/// Helper to apply pending EF Core migrations on startup safely.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task MigrateDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ShortLinkDbContext>>();
        var context = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

        try
        {
            logger.LogInformation("Checking for pending PostgreSQL migrations...");
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("PostgreSQL database is already up to date.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply database migrations on startup (PostgreSQL may be offline or credentials incorrect). Application will continue.");
        }
    }
}
