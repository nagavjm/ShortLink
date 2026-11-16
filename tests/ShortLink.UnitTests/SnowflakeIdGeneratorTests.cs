using System.Collections.Concurrent;
using ShortLink.Infrastructure.Services;
using Xunit;

namespace ShortLink.UnitTests;

public class SnowflakeIdGeneratorTests
{
    [Fact]
    public void NextId_GeneratesPositiveUniqueIds()
    {
        var generator = new SnowflakeIdGenerator(machineId: 1);
        var id1 = generator.NextId();
        var id2 = generator.NextId();

        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.True(id2 > id1, "Snowflake IDs must be strictly monotonically increasing");
    }

    [Fact]
    public void NextId_ConcurrentCalls_ZeroCollisions()
    {
        var generator = new SnowflakeIdGenerator(machineId: 1);
        var idBag = new ConcurrentBag<long>();
        const int totalTasks = 20;
        const int iterationsPerTask = 1000;

        Parallel.For(0, totalTasks, _ =>
        {
            for (int i = 0; i < iterationsPerTask; i++)
            {
                idBag.Add(generator.NextId());
            }
        });

        var distinctCount = idBag.Distinct().Count();
        Assert.Equal(totalTasks * iterationsPerTask, distinctCount);
    }

    [Fact]
    public void Snowflake_Base62Encoding_ProducesUniqueCompactCodes()
    {
        var snowflake = new SnowflakeIdGenerator(machineId: 1);
        var base62 = new Base62Service();

        var codes = new HashSet<string>();
        for (int i = 0; i < 5000; i++)
        {
            var id = snowflake.NextId();
            var code = base62.Encode(id);

            Assert.True(codes.Add(code), $"Collision detected for Snowflake ID {id} -> {code}");
            Assert.True(code.Length >= 8, "Snowflake Base62 codes are typically 8-11 characters");
        }
    }
}
