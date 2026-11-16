namespace ShortLink.Application.DTOs;

/// <summary>
/// DTO representing the incoming client payload to create a shortened URL.
/// </summary>
public record CreateUrlRequestDto
{
    public string OriginalUrl { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
}
