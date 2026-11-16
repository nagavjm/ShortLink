using System.Runtime.CompilerServices;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Domain.Entities;
using ShortLink.Domain.Interfaces;

namespace ShortLink.UnitTests.Fakes;

public class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}

public class FakeUrlCacheService : IUrlCacheService
{
    public Dictionary<string, string> Store { get; } = new();
    public HashSet<string> RemovedKeys { get; } = new();

    public Task<string?> GetOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        Store.TryGetValue($"url:{shortCode}", out var val);
        return Task.FromResult<string?>(val);
    }

    public Task SetOriginalUrlAsync(string shortCode, string originalUrl, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        Store[$"url:{shortCode}"] = originalUrl;
        return Task.CompletedTask;
    }

    public Task RemoveOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        Store.Remove($"url:{shortCode}");
        RemovedKeys.Add(shortCode);
        return Task.CompletedTask;
    }

    public Task SetNegativeCacheAsync(string shortCode, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        Store[$"url:{shortCode}"] = string.Empty; // Negative cache representation
        return Task.CompletedTask;
    }

    public async Task<string?> GetOrSetWithStampedeProtectionAsync(
        string shortCode,
        Func<Task<string?>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetOriginalUrlAsync(shortCode, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var result = await factory();
        if (result != null)
        {
            await SetOriginalUrlAsync(shortCode, result, ttl, cancellationToken);
        }
        else
        {
            await SetNegativeCacheAsync(shortCode, TimeSpan.FromMinutes(5), cancellationToken);
        }

        return result;
    }
}

public class FakeClickAnalyticsQueue : IClickAnalyticsQueue
{
    public List<string> EnqueuedClicks { get; } = new();

    public ValueTask EnqueueClickAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        EnqueuedClicks.Add(shortCode);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<string> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var click in EnqueuedClicks)
        {
            yield return click;
        }
        await Task.CompletedTask;
    }
}

public class FakeUrlMappingRepository : IUrlMappingRepository
{
    public Dictionary<string, UrlMapping> ByShortCode { get; } = new();
    public Dictionary<long, UrlMapping> ById { get; } = new();
    private long _sequence = 1000;

    public Task<UrlMapping?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        ById.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<UrlMapping?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        ByShortCode.TryGetValue(shortCode, out var entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<UrlMapping>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UrlMapping> list = ByShortCode.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<(IReadOnlyList<UrlMapping> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = ByShortCode.Values.OrderByDescending(u => u.CreatedAt).ToList();
        var total = all.Count;
        var paged = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<UrlMapping> Items, int TotalCount)>((paged, total));
    }

    public Task<bool> ExistsAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ByShortCode.ContainsKey(shortCode));
    }

    public Task AddAsync(UrlMapping urlMapping, CancellationToken cancellationToken = default)
    {
        ById[urlMapping.Id] = urlMapping;
        ByShortCode[urlMapping.ShortCode] = urlMapping;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UrlMapping urlMapping, CancellationToken cancellationToken = default)
    {
        ById[urlMapping.Id] = urlMapping;
        ByShortCode[urlMapping.ShortCode] = urlMapping;
        return Task.CompletedTask;
    }

    public Task IncrementClickCountAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        if (ByShortCode.TryGetValue(shortCode, out var mapping))
        {
            mapping.IncrementClickCount();
        }
        return Task.CompletedTask;
    }

    public Task<long> GetNextSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(++_sequence);
    }
}
