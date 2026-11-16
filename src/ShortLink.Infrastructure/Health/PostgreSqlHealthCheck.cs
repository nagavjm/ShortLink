using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShortLink.Infrastructure.Data;

namespace ShortLink.Infrastructure.Health;

/// <summary>
/// Probes PostgreSQL database responsiveness using a lightweight SELECT 1 query.
/// Measures database query round-trip latency.
/// </summary>
public class PostgreSqlHealthCheck : IHealthCheck
{
    private readonly ShortLinkDbContext _context;

    public PostgreSqlHealthCheck(ShortLinkDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute lightweight query against database
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            if (canConnect)
            {
                var data = new Dictionary<string, object>
                {
                    ["Provider"] = "Npgsql.EntityFrameworkCore.PostgreSQL",
                    ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds
                };

                return HealthCheckResult.Healthy(
                    $"PostgreSQL is responsive ({stopwatch.ElapsedMilliseconds}ms).",
                    data);
            }

            return HealthCheckResult.Unhealthy(
                "PostgreSQL CanConnectAsync returned false.",
                data: new Dictionary<string, object> { ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy(
                $"PostgreSQL connectivity check failed: {ex.Message}",
                ex,
                new Dictionary<string, object> { ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds });
        }
    }
}
