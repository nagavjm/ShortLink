using Microsoft.Extensions.Logging.Abstractions;
using ShortLink.Application.Common.Exceptions;
using ShortLink.Application.DTOs;
using ShortLink.Application.Services;
using ShortLink.Application.Validators;
using ShortLink.Domain.Entities;
using ShortLink.Infrastructure.Services;
using ShortLink.UnitTests.Fakes;
using Xunit;

namespace ShortLink.UnitTests;

public class UrlShortenerServiceTests
{
    private readonly FakeUrlMappingRepository _repository = new();
    private readonly FakeUrlCacheService _cacheService = new();
    private readonly FakeClickAnalyticsQueue _clickQueue = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly Base62Service _base62Service = new();
    private readonly AutoIncrementShortCodeGenerator _shortCodeGenerator;
    private readonly CreateUrlRequestValidator _validator;
    private readonly UrlShortenerService _sut;

    public UrlShortenerServiceTests()
    {
        _shortCodeGenerator = new AutoIncrementShortCodeGenerator(_base62Service);
        _validator = new CreateUrlRequestValidator(_dateTimeProvider);
        _sut = new UrlShortenerService(
            _repository,
            _cacheService,
            _shortCodeGenerator,
            _clickQueue,
            _dateTimeProvider,
            _validator,
            NullLogger<UrlShortenerService>.Instance);
    }

    [Fact]
    public async Task CreateShortUrl_ValidRequest_CreatesMapping_WarmsCache_AndReturnsDto()
    {
        var request = new CreateUrlRequestDto
        {
            OriginalUrl = "https://www.google.com/search?q=dotnet",
            ExpiresAt = _dateTimeProvider.UtcNow.AddDays(7)
        };

        var result = await _sut.CreateShortUrlAsync(request, "https://short.link");

        Assert.NotNull(result);
        Assert.NotEmpty(result.ShortCode);
        Assert.Equal("https://short.link/" + result.ShortCode, result.ShortUrl);
        Assert.Equal(request.OriginalUrl, result.OriginalUrl);
        Assert.True(result.IsActive);

        // Verify entity persisted in repository
        var saved = await _repository.GetByShortCodeAsync(result.ShortCode);
        Assert.NotNull(saved);

        // Verify Redis cache warmed
        var cached = await _cacheService.GetOriginalUrlAsync(result.ShortCode);
        Assert.Equal(request.OriginalUrl, cached);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://files.example.com")]
    public async Task CreateShortUrl_InvalidUrl_ThrowsValidationException(string invalidUrl)
    {
        var request = new CreateUrlRequestDto
        {
            OriginalUrl = invalidUrl
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateShortUrlAsync(request, "https://short.link"));
    }

    [Fact]
    public async Task CreateShortUrl_ExpirationInPast_ThrowsValidationException()
    {
        var request = new CreateUrlRequestDto
        {
            OriginalUrl = "https://example.com",
            ExpiresAt = _dateTimeProvider.UtcNow.AddHours(-1)
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateShortUrlAsync(request, "https://short.link"));
    }

    [Fact]
    public async Task GetOriginalUrl_CacheHit_ReturnsFromCache_AndEnqueuesClick()
    {
        const string shortCode = "aB92xK";
        const string originalUrl = "https://example.com/cached-page";

        await _cacheService.SetOriginalUrlAsync(shortCode, originalUrl, TimeSpan.FromHours(1));

        var result = await _sut.GetOriginalUrlAndTrackClickAsync(shortCode);

        Assert.Equal(originalUrl, result);
        Assert.Contains(shortCode, _clickQueue.EnqueuedClicks);
        Assert.Empty(_repository.ByShortCode); // Database was never hit!
    }

    [Fact]
    public async Task GetOriginalUrl_CacheMiss_FetchesFromDb_WarmsCache_AndEnqueuesClick()
    {
        const string shortCode = "k8M2pL";
        const string originalUrl = "https://example.com/db-page";

        var entity = UrlMapping.Create(originalUrl, _dateTimeProvider.UtcNow);
        entity.SetShortCode(shortCode);
        await _repository.AddAsync(entity);

        var result = await _sut.GetOriginalUrlAndTrackClickAsync(shortCode);

        Assert.Equal(originalUrl, result);
        Assert.Contains(shortCode, _clickQueue.EnqueuedClicks);

        // Verify Redis was populated on cache miss
        var cached = await _cacheService.GetOriginalUrlAsync(shortCode);
        Assert.Equal(originalUrl, cached);
    }

    [Fact]
    public async Task GetOriginalUrl_NonExistent_ThrowsNotFoundException_AndSetsNegativeCache()
    {
        const string nonExistentCode = "notFound99";

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetOriginalUrlAndTrackClickAsync(nonExistentCode));

        // Verify negative cache was set
        var cached = await _cacheService.GetOriginalUrlAsync(nonExistentCode);
        Assert.Equal(string.Empty, cached);
    }

    [Fact]
    public async Task GetOriginalUrl_ExpiredUrl_ThrowsNotFoundException()
    {
        const string expiredCode = "exp123";
        var expiredEntity = UrlMapping.Create("https://example.com/expired",
            _dateTimeProvider.UtcNow.AddDays(-2),
            _dateTimeProvider.UtcNow.AddDays(-1));
        expiredEntity.SetShortCode(expiredCode);
        await _repository.AddAsync(expiredEntity);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetOriginalUrlAndTrackClickAsync(expiredCode));
    }

    [Fact]
    public async Task DeactivateUrl_MarksEntityInactive_AndInvalidatesRedisCache()
    {
        const string shortCode = "act001";
        var entity = UrlMapping.Create("https://example.com/active", _dateTimeProvider.UtcNow);
        entity.SetShortCode(shortCode);
        await _repository.AddAsync(entity);
        await _cacheService.SetOriginalUrlAsync(shortCode, entity.OriginalUrl, TimeSpan.FromHours(1));

        var deactivated = await _sut.DeactivateUrlAsync(shortCode);

        Assert.True(deactivated);
        Assert.False(entity.IsActive);
        Assert.Contains(shortCode, _cacheService.RemovedKeys);
    }
}
