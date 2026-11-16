namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Abstraction for Redis-backed URL caching implementing Cache-Aside pattern,
/// negative caching, and stampede prevention.
/// Key format: url:{shortCode}
/// </summary>
public interface IUrlCacheService
{
    /// <summary>
    /// Retrieves cached target URL by short code.
    /// Returns null on cache miss or when Redis is unavailable (fallback trigger).
    /// </summary>
    Task<string?> GetOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets target URL in cache with specified TTL.
    /// </summary>
    Task SetOriginalUrlAsync(string shortCode, string originalUrl, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached URL when deactivated or modified.
    /// </summary>
    Task RemoveOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets negative cache entry when a short code is proven not to exist,
    /// preventing repeated DB hits (penetration attack mitigation).
    /// </summary>
    Task SetNegativeCacheAsync(string shortCode, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache-Aside with distributed mutex protection against Cache Stampede (Thundering Herd).
    /// Ensures only one thread hits the database on a cache miss for a hot key.
    /// </summary>
    Task<string?> GetOrSetWithStampedeProtectionAsync(
        string shortCode,
        Func<Task<string?>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
