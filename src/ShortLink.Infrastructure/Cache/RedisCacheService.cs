using Microsoft.Extensions.Logging;
using ShortLink.Application.Common.Interfaces;
using StackExchange.Redis;

namespace ShortLink.Infrastructure.Cache;

/// <summary>
/// Redis Cache service implementation implementing Cache-Aside pattern,
/// negative caching, and graceful failure degradation to database.
/// Redis Key schema: url:{shortCode}
/// </summary>
public class RedisCacheService : IUrlCacheService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private const string KeyPrefix = "url:";
    private const string NegativeCacheSentinel = "__SHORTLINK_NOT_FOUND__";

    public RedisCacheService(IConnectionMultiplexer? redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase? GetDatabase()
    {
        try
        {
            if (_redis != null && _redis.IsConnected)
            {
                return _redis.GetDatabase();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis connection failed. Falling back gracefully to PostgreSQL.");
        }

        return null;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db == null)
        {
            // Redis is down or unavailable - return null to trigger PostgreSQL fallback
            return null;
        }

        try
        {
            var key = $"{KeyPrefix}{shortCode}";
            RedisValue value = await db.StringGetAsync(key);

            if (!value.HasValue)
            {
                return null; // Cache miss
            }

            if (value == NegativeCacheSentinel)
            {
                // Negative cache hit - URL is known to not exist / inactive
                return string.Empty;
            }

            return value.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read error for key {KeyPrefix}{ShortCode}. Proceeding with DB fallback.", KeyPrefix, shortCode);
            return null;
        }
    }

    public async Task SetOriginalUrlAsync(string shortCode, string originalUrl, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db == null) return;

        try
        {
            var key = $"{KeyPrefix}{shortCode}";
            await db.StringSetAsync(key, originalUrl, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write error for key {KeyPrefix}{ShortCode}.", KeyPrefix, shortCode);
        }
    }

    public async Task RemoveOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db == null) return;

        try
        {
            var key = $"{KeyPrefix}{shortCode}";
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis delete error for key {KeyPrefix}{ShortCode}.", KeyPrefix, shortCode);
        }
    }

    public async Task SetNegativeCacheAsync(string shortCode, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db == null) return;

        try
        {
            var key = $"{KeyPrefix}{shortCode}";
            await db.StringSetAsync(key, NegativeCacheSentinel, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis negative cache write error for key {KeyPrefix}{ShortCode}.", KeyPrefix, shortCode);
        }
    }
}
