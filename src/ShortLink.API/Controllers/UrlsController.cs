using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Application.DTOs;

namespace ShortLink.API.Controllers;

/// <summary>
/// URL management controller handling creation, retrieval, and deactivation.
/// </summary>
public class UrlsController : ApiControllerBase
{
    private readonly IUrlShortenerService _urlShortenerService;

    public UrlsController(IUrlShortenerService urlShortenerService)
    {
        _urlShortenerService = urlShortenerService;
    }

    /// <summary>
    /// Creates a shortened URL from the provided destination URL and optional expiration.
    /// </summary>
    /// <param name="request">Payload containing original URL and expiration date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 201 Created with UrlResponseDto.</returns>
    [HttpPost]
    [EnableRateLimiting("UrlCreationPolicy")]
    [Consumes(MediaTypeNames.Application.Json)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(UrlResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateShortUrl(
        [FromBody] CreateUrlRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _urlShortenerService.CreateShortUrlAsync(request, GetBaseUrl(), cancellationToken);
        return CreatedAtAction(nameof(GetUrlByShortCode), new { shortCode = result.ShortCode }, result);
    }

    /// <summary>
    /// Retrieves detailed metadata for a specific short code.
    /// </summary>
    [HttpGet("{shortCode}")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(UrlResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUrlByShortCode(
        string shortCode,
        CancellationToken cancellationToken)
    {
        var result = await _urlShortenerService.GetUrlByShortCodeAsync(shortCode, GetBaseUrl(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a paginated list of all shortened URLs.
    /// Supports high-volume pagination to prevent memory exhaustion.
    /// </summary>
    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(PagedResult<UrlResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUrls(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _urlShortenerService.GetPagedUrlsAsync(pageNumber, pageSize, GetBaseUrl(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Inspects the live Redis cache entry for a given short code.
    /// Useful for verifying cache hits, negative cache sentinels, and cache invalidation.
    /// </summary>
    [HttpGet("cache/{shortCode}")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectCache(
        string shortCode,
        [FromServices] IUrlCacheService cacheService,
        CancellationToken cancellationToken)
    {
        var cachedUrl = await cacheService.GetOriginalUrlAsync(shortCode, cancellationToken);
        return Ok(new
        {
            ShortCode = shortCode,
            CacheKey = $"url:{shortCode}",
            IsCached = cachedUrl != null,
            IsNegativeCache = cachedUrl == string.Empty,
            CachedValue = cachedUrl == string.Empty ? "(Negative Cache Sentinel)" : cachedUrl
        });
    }

    /// <summary>
    /// Deactivates a shortened URL and invalidates its cache.
    /// </summary>
    [HttpDelete("{shortCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUrl(
        string shortCode,
        CancellationToken cancellationToken)
    {
        await _urlShortenerService.DeactivateUrlAsync(shortCode, cancellationToken);
        return NoContent();
    }
}
