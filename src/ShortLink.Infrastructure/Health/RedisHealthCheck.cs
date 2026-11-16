using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ShortLink.Infrastructure.Health;

/// <summary>
/// Probes Redis in-memory cache responsiveness using an asynchronous PING command.
/// Reports 'Degraded' rather than 'Unhealthy' when Redis is unavailable,
/// reflecting our architecture's graceful database fallback capability.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;

    public RedisHealthCheck(IConnectionMultiplexer? redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            return HealthCheckResult.Degraded(
                "Redis is not connected. Application is operating in graceful direct-database fallback mode.",
                data: new Dictionary<string, object>
                {
                    ["Status"] = "Disconnected",
                    ["Fallback"] = "PostgreSQL Direct"
                });
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var db = _redis.GetDatabase();
            var pingLatency = await db.PingAsync();
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["ServerEndpoints"] = string.Join(", ", _redis.GetEndPoints().Select(e => e.ToString())),
                ["PingLatencyMs"] = pingLatency.TotalMilliseconds,
                ["RoundTripMs"] = stopwatch.ElapsedMilliseconds
            };

            return HealthCheckResult.Healthy(
                $"Redis is responsive (Ping: {pingLatency.TotalMilliseconds:F1}ms).",
                data);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Degraded(
                $"Redis PING probe failed: {ex.Message}. Falling back to PostgreSQL.",
                ex,
                new Dictionary<string, object> { ["RoundTripMs"] = stopwatch.ElapsedMilliseconds });
        }
    }
}
