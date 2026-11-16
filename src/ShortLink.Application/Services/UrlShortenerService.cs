using FluentValidation;
using Microsoft.Extensions.Logging;
using ShortLink.Application.Common.Exceptions;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Application.DTOs;
using ShortLink.Domain.Entities;
using ShortLink.Domain.Interfaces;

namespace ShortLink.Application.Services;

/// <summary>
/// Primary use-case service orchestrating URL creation, resolution, cache-aside lookups,
/// cache stampede prevention, and asynchronous click tracking.
/// </summary>
public class UrlShortenerService : IUrlShortenerService
{
    private readonly IUrlMappingRepository _repository;
    private readonly IUrlCacheService _cacheService;
    private readonly IShortCodeGenerator _shortCodeGenerator;
    private readonly IClickAnalyticsQueue _clickQueue;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateUrlRequestDto> _validator;
    private readonly ILogger<UrlShortenerService> _logger;

    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(5);

    public UrlShortenerService(
        IUrlMappingRepository repository,
        IUrlCacheService cacheService,
        IShortCodeGenerator shortCodeGenerator,
        IClickAnalyticsQueue clickQueue,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateUrlRequestDto> validator,
        ILogger<UrlShortenerService> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _shortCodeGenerator = shortCodeGenerator;
        _clickQueue = clickQueue;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UrlResponseDto> CreateShortUrlAsync(
        CreateUrlRequestDto request,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        // 1. FluentValidation rule check
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new Common.Exceptions.ValidationException(validationResult.Errors);
        }

        // 2. Fetch next sequence ID from PostgreSQL sequence
        var nextId = await _repository.GetNextSequenceValueAsync(cancellationToken);

        // 3. Generate Base62 short code from identity
        var shortCode = _shortCodeGenerator.Generate(nextId);

        // 4. Create and persist Domain Entity
        var entity = UrlMapping.Create(request.OriginalUrl, _dateTimeProvider.UtcNow, request.ExpiresAt);
        entity.SetShortCode(shortCode);

        await _repository.AddAsync(entity, cancellationToken);

        _logger.LogInformation("Created short URL '{ShortCode}' for '{OriginalUrl}' with ID {Id}.",
            shortCode, request.OriginalUrl, entity.Id);

        // 5. Populate Redis Cache upfront (Cache Warming)
        await _cacheService.SetOriginalUrlAsync(shortCode, entity.OriginalUrl, DefaultCacheTtl, cancellationToken);

        return MapToDto(entity, baseUrl);
    }

    public async Task<string> GetOriginalUrlAndTrackClickAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        // Resolve original URL using Cache-Aside with Distributed Lock Stampede Protection
        var originalUrl = await _cacheService.GetOrSetWithStampedeProtectionAsync(
            shortCode,
            async () =>
            {
                var mapping = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
                if (mapping == null || !mapping.IsActive)
                {
                    return null;
                }

                if (mapping.IsExpired(_dateTimeProvider.UtcNow))
                {
                    return null;
                }

                return mapping.OriginalUrl;
            },
            DefaultCacheTtl,
            cancellationToken);

        if (string.IsNullOrEmpty(originalUrl))
        {
            throw new NotFoundException($"Short link '{shortCode}' was not found, is inactive, or has expired.");
        }

        // Enqueue non-blocking click tracking
        await _clickQueue.EnqueueClickAsync(shortCode, cancellationToken);

        return originalUrl;
    }

    public async Task<UrlResponseDto> GetUrlByShortCodeAsync(
        string shortCode,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
        if (mapping == null)
        {
            throw new NotFoundException(nameof(UrlMapping), shortCode);
        }

        return MapToDto(mapping, baseUrl);
    }

    public async Task<IReadOnlyList<UrlResponseDto>> GetAllUrlsAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var mappings = await _repository.GetAllAsync(cancellationToken);
        return mappings.Select(m => MapToDto(m, baseUrl)).ToList();
    }

    public async Task<bool> DeactivateUrlAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _repository.GetByShortCodeAsync(shortCode, cancellationToken);
        if (mapping == null)
        {
            throw new NotFoundException(nameof(UrlMapping), shortCode);
        }

        mapping.Deactivate();
        await _repository.UpdateAsync(mapping, cancellationToken);

        // Invalidate Redis cache & place negative cache
        await _cacheService.RemoveOriginalUrlAsync(shortCode, cancellationToken);
        await _cacheService.SetNegativeCacheAsync(shortCode, NegativeCacheTtl, cancellationToken);

        _logger.LogInformation("Short link '{ShortCode}' deactivated successfully.", shortCode);
        return true;
    }

    private static UrlResponseDto MapToDto(UrlMapping entity, string baseUrl)
    {
        var formattedBase = baseUrl.TrimEnd('/');
        return new UrlResponseDto
        {
            Id = entity.Id,
            ShortCode = entity.ShortCode,
            ShortUrl = $"{formattedBase}/{entity.ShortCode}",
            OriginalUrl = entity.OriginalUrl,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
            ClickCount = entity.ClickCount,
            IsActive = entity.IsActive
        };
    }
}
