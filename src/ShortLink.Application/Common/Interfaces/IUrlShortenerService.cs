using ShortLink.Application.DTOs;

namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Primary use-case service interface orchestrating URL creation, resolution, retrieval, and deactivation.
/// </summary>
public interface IUrlShortenerService
{
    Task<UrlResponseDto> CreateShortUrlAsync(CreateUrlRequestDto request, string baseUrl, CancellationToken cancellationToken = default);

    Task<string> GetOriginalUrlAndTrackClickAsync(string shortCode, CancellationToken cancellationToken = default);

    Task<UrlResponseDto> GetUrlByShortCodeAsync(string shortCode, string baseUrl, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UrlResponseDto>> GetAllUrlsAsync(string baseUrl, CancellationToken cancellationToken = default);

    Task<bool> DeactivateUrlAsync(string shortCode, CancellationToken cancellationToken = default);
}
