using ShortLink.Domain.Common;
using ShortLink.Domain.Exceptions;

namespace ShortLink.Domain.Entities;

/// <summary>
/// Core aggregate entity representing a shortened URL mapping.
/// Follows Rich Domain Model principles where state changes occur via domain methods.
/// </summary>
public class UrlMapping : BaseEntity
{
    public string ShortCode { get; private set; } = string.Empty;
    public string OriginalUrl { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public long ClickCount { get; private set; }
    public bool IsActive { get; private set; }

    // Required by EF Core
    private UrlMapping()
    {
    }

    private UrlMapping(string originalUrl, DateTime createdAt, DateTime? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            throw new DomainException("Original URL cannot be empty.");
        }

        if (expiresAt.HasValue && expiresAt.Value <= createdAt)
        {
            throw new DomainException("Expiration date must be in the future relative to creation date.");
        }

        OriginalUrl = originalUrl;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        ClickCount = 0;
        IsActive = true;
    }

    /// <summary>
    /// Factory method to create a new UrlMapping instance before short code generation.
    /// </summary>
    public static UrlMapping Create(string originalUrl, DateTime utcNow, DateTime? expiresAtUtc = null)
    {
        return new UrlMapping(originalUrl, utcNow, expiresAtUtc);
    }

    /// <summary>
    /// Assigns the generated unique Base62 short code to the entity.
    /// </summary>
    public void SetShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            throw new DomainException("Short code cannot be empty.");
        }

        ShortCode = shortCode;
    }

    /// <summary>
    /// Deactivates the URL mapping so it can no longer be accessed.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Increments the total redirect/click count.
    /// </summary>
    public void IncrementClickCount()
    {
        ClickCount++;
    }

    /// <summary>
    /// Determines whether the URL is expired based on current UTC time.
    /// </summary>
    public bool IsExpired(DateTime utcNow)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= utcNow;
    }
}
