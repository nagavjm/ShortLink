using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Domain.Entities;

namespace ShortLink.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for ShortLink application targeting PostgreSQL.
/// Implements IApplicationDbContext for inversion of control.
/// </summary>
public class ShortLinkDbContext : DbContext, IApplicationDbContext
{
    public ShortLinkDbContext(DbContextOptions<ShortLinkDbContext> options)
        : base(options)
    {
    }

    public DbSet<UrlMapping> UrlMappings => Set<UrlMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Automatically scan and apply all IEntityTypeConfiguration classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
