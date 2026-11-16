using Microsoft.AspNetCore.Mvc;
using ShortLink.Application.Common.Interfaces;

namespace ShortLink.API.Controllers;

/// <summary>
/// High-throughput redirect controller servicing shortened URL resolutions.
/// Returns HTTP 302 Found to enforce non-cached, server-tracked redirects.
/// </summary>
[ApiController]
[Route("")]
public class RedirectController : ControllerBase
{
    private readonly IUrlShortenerService _urlShortenerService;

    public RedirectController(IUrlShortenerService urlShortenerService)
    {
        _urlShortenerService = urlShortenerService;
    }

    /// <summary>
    /// Resolves the short code and redirects the client to the original destination URL.
    /// Uses HTTP 302 Found to ensure click analytics are recorded on every visit.
    /// </summary>
    /// <param name="shortCode">The unique Base62 short code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 302 Found redirect to original destination.</returns>
    [HttpGet("{shortCode:regex(^[a-zA-Z0-9]{{1,16}}$)}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedirectToOriginal(
        string shortCode,
        CancellationToken cancellationToken)
    {
        var originalUrl = await _urlShortenerService.GetOriginalUrlAndTrackClickAsync(shortCode, cancellationToken);

        // Prevent client-side browser caching to maintain accurate analytics & enable instant link deactivation
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        // ASP.NET Core Redirect produces HTTP 302 Found
        return Redirect(originalUrl);
    }
}
