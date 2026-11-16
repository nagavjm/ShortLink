using System.Threading.RateLimiting;
using Xunit;

namespace ShortLink.UnitTests;

public class RateLimiterTests
{
    [Fact]
    public void FixedWindowRateLimiter_AllowsConfiguredPermitLimit()
    {
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        };

        using var limiter = new FixedWindowRateLimiter(options);

        // First 100 requests must succeed
        for (int i = 0; i < 100; i++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired, $"Request {i + 1} should have been allowed under the limit of 100.");
        }

        // 101st request must be rejected
        using var rejectedLease = limiter.AttemptAcquire();
        Assert.False(rejectedLease.IsAcquired, "Request 101 must be rejected when limit is exceeded.");
    }

    [Fact]
    public void PartitionedRateLimiter_IsolatesLimitsByClientIp()
    {
        var partitioner = PartitionedRateLimiter.Create<string, string>(key =>
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: key,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 2,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });

        const string clientA = "192.168.1.100";
        const string clientB = "192.168.1.200";

        // Client A uses up quota (2 requests)
        using var leaseA1 = partitioner.AttemptAcquire(clientA);
        using var leaseA2 = partitioner.AttemptAcquire(clientA);
        using var leaseA3 = partitioner.AttemptAcquire(clientA);

        Assert.True(leaseA1.IsAcquired);
        Assert.True(leaseA2.IsAcquired);
        Assert.False(leaseA3.IsAcquired, "Client A exceeded quota");

        // Client B has its own independent partition and quota
        using var leaseB1 = partitioner.AttemptAcquire(clientB);
        using var leaseB2 = partitioner.AttemptAcquire(clientB);

        Assert.True(leaseB1.IsAcquired, "Client B should not be affected by Client A's quota exhaustion");
        Assert.True(leaseB2.IsAcquired);
    }
}
