using Microsoft.EntityFrameworkCore;
using ShortLink.Domain.Entities;

namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Abstraction for the Entity Framework Core DbContext consumed by Application layer.
/// Decouples application logic from database provider details.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<UrlMapping> UrlMappings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
