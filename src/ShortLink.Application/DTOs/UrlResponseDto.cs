namespace ShortLink.Application.DTOs;

/// <summary>
/// DTO representing the response payload containing URL details.
/// </summary>
public record UrlResponseDto
{
    public long Id { get; init; }
    public string ShortCode { get; init; } = string.Empty;
    public string ShortUrl { get; init; } = string.Empty;
    public string OriginalUrl { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public long ClickCount { get; init; }
    public bool IsActive { get; init; }
}
