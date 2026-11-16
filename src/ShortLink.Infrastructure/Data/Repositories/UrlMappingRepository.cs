using Microsoft.EntityFrameworkCore;
using ShortLink.Domain.Entities;
using ShortLink.Domain.Interfaces;

namespace ShortLink.Infrastructure.Data.Repositories;

/// <summary>
/// Production PostgreSQL repository implementation for UrlMapping.
/// Employs AsNoTracking for read performance, compiled execution, and atomic raw SQL updates.
/// </summary>
public class UrlMappingRepository : IUrlMappingRepository
{
    private readonly ShortLinkDbContext _context;

    public UrlMappingRepository(ShortLinkDbContext context)
    {
        _context = context;
    }

    public async Task<UrlMapping?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.UrlMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<UrlMapping?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        return await _context.UrlMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken);
    }

    public async Task<IReadOnlyList<UrlMapping>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UrlMappings
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        return await _context.UrlMappings
            .AnyAsync(u => u.ShortCode == shortCode, cancellationToken);
    }

    public async Task AddAsync(UrlMapping urlMapping, CancellationToken cancellationToken = default)
    {
        await _context.UrlMappings.AddAsync(urlMapping, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UrlMapping urlMapping, CancellationToken cancellationToken = default)
    {
        _context.UrlMappings.Update(urlMapping);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes an atomic single-statement SQL update to increment ClickCount.
    /// Avoids concurrency race conditions and removes the need for row-level locking or optimistic locks.
    /// </summary>
    public async Task IncrementClickCountAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"UrlMappings\" SET \"ClickCount\" = \"ClickCount\" + 1 WHERE \"ShortCode\" = {shortCode}",
            cancellationToken);
    }
}
