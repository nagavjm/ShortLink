using ShortLink.Domain.Entities;

namespace ShortLink.Domain.Interfaces;

/// <summary>
/// Domain Repository abstraction for UrlMapping aggregate root.
/// Isolates data access operations from business use cases.
/// </summary>
public interface IUrlMappingRepository
{
    Task<UrlMapping?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<UrlMapping?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UrlMapping>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string shortCode, CancellationToken cancellationToken = default);

    Task AddAsync(UrlMapping urlMapping, CancellationToken cancellationToken = default);

    Task UpdateAsync(UrlMapping urlMapping, CancellationToken cancellationToken = default);

    Task IncrementClickCountAsync(string shortCode, CancellationToken cancellationToken = default);
}
