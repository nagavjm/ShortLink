using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShortLink.Infrastructure.Health;
using Xunit;

namespace ShortLink.UnitTests;

public class HealthCheckTests
{
    [Fact]
    public async Task RedisHealthCheck_WhenMultiplexerIsNull_ReturnsDegraded()
    {
        var healthCheck = new RedisHealthCheck(null);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("fallback", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Data.ContainsKey("Fallback"));
    }
}
