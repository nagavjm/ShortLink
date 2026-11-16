using Microsoft.Extensions.Logging;
using ShortLink.Application.Common.Interfaces;
using StackExchange.Redis;

namespace ShortLink.Infrastructure.Cache;

/// <summary>
/// Redis Cache service implementation implementing Cache-Aside pattern,
/// negative caching, distributed lock stampede protection, and graceful failure degradation.
/// Redis Key schema: url:{shortCode}
/// Lock Key schema: lock:url:{shortCode}
/// </summary>
public class RedisCacheService : IUrlCacheService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheService> _logger;

    private const string KeyPrefix = "url:";
    private const string LockPrefix = "lock:url:";
    public const string NegativeCacheSentinel = "__SHORTLINK_NOT_FOUND__";

    // Lua script for atomic unlock (checks token ownership before deletion)
    private const string UnlockLuaScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end";

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
            _logger.LogWarning(ex, "Redis connection check failed. Falling back gracefully to PostgreSQL.");
        }

        return null;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db == null) return null;

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
                // Negative cache hit - URL known to not exist or be deactivated
                return string.Empty;
            }

            return value.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read error for key {KeyPrefix}{ShortCode}. Fallback to PostgreSQL.", KeyPrefix, shortCode);
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

    /// <summary>
    /// Implements Cache-Aside pattern combined with Redis distributed locking
    /// to eliminate Cache Stampede (Thundering Herd / Dog-Piling).
    /// </summary>
    public async Task<string?> GetOrSetWithStampedeProtectionAsync(
        string shortCode,
        Func<Task<string?>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        // 1. Initial Cache Check
        var cached = await GetOriginalUrlAsync(shortCode, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var db = GetDatabase();
        if (db == null)
        {
            // Redis offline: invoke database factory directly
            return await factory();
        }

        var lockKey = $"{LockPrefix}{shortCode}";
        var lockToken = Guid.NewGuid().ToString("N");
        var lockTtl = TimeSpan.FromSeconds(5);

        bool lockAcquired = false;
        try
        {
            lockAcquired = await db.StringSetAsync(lockKey, lockToken, lockTtl, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis distributed lock acquisition error. Calling factory directly.");
            return await factory();
        }

        if (lockAcquired)
        {
            try
            {
                // Double-checked locking across distributed nodes
                cached = await GetOriginalUrlAsync(shortCode, cancellationToken);
                if (cached != null)
                {
                    return cached;
                }

                // Execute DB factory
                var originalUrl = await factory();

                if (!string.IsNullOrEmpty(originalUrl))
                {
                    await SetOriginalUrlAsync(shortCode, originalUrl, ttl, cancellationToken);
                }
                else
                {
                    // Negative caching (5 min TTL)
                    await SetNegativeCacheAsync(shortCode, TimeSpan.FromMinutes(5), cancellationToken);
                }

                return originalUrl;
            }
            finally
            {
                // Atomic unlock using Lua script to guarantee only lock owner can release
                try
                {
                    await db.ScriptEvaluateAsync(
                        UnlockLuaScript,
                        new RedisKey[] { lockKey },
                        new RedisValue[] { lockToken });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis distributed unlock error for key {LockKey}.", lockKey);
                }
            }
        }
        else
        {
            // Lock not acquired: Another thread is currently loading the value from database.
            // Spin/wait briefly with retries to read the newly populated cache.
            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                await Task.Delay(50, cancellationToken);
                cached = await GetOriginalUrlAsync(shortCode, cancellationToken);
                if (cached != null)
                {
                    return cached;
                }
            }

            // Fallback: If still not populated after spin-wait, invoke factory directly
            return await factory();
        }
    }
}
