using Microsoft.Extensions.Logging.Abstractions;
using ShortLink.Infrastructure.Cache;
using Xunit;

namespace ShortLink.UnitTests;

public class RedisCacheServiceTests
{
    [Fact]
    public async Task GetOriginalUrlAsync_WhenRedisIsNull_ReturnsNullGracefully()
    {
        var sut = new RedisCacheService(null, NullLogger<RedisCacheService>.Instance);

        var result = await sut.GetOriginalUrlAsync("testCode");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetOriginalUrlAsync_WhenRedisIsNull_DoesNotThrow()
    {
        var sut = new RedisCacheService(null, NullLogger<RedisCacheService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sut.SetOriginalUrlAsync("testCode", "https://example.com", TimeSpan.FromHours(1)));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveOriginalUrlAsync_WhenRedisIsNull_DoesNotThrow()
    {
        var sut = new RedisCacheService(null, NullLogger<RedisCacheService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sut.RemoveOriginalUrlAsync("testCode"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetOrSetWithStampedeProtectionAsync_WhenRedisIsNull_InvokesFactoryDirectly()
    {
        var sut = new RedisCacheService(null, NullLogger<RedisCacheService>.Instance);
        var factoryExecuted = false;

        var result = await sut.GetOrSetWithStampedeProtectionAsync(
            "testCode",
            () =>
            {
                factoryExecuted = true;
                return Task.FromResult<string?>("https://example.com/fallback");
            },
            TimeSpan.FromHours(1));

        Assert.True(factoryExecuted);
        Assert.Equal("https://example.com/fallback", result);
    }
}
