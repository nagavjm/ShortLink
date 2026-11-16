using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShortLink.Domain.Entities;

namespace ShortLink.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core Fluent API configuration for UrlMapping entity.
/// Configures PostgreSQL data types, column constraints, primary key generation, and B-Tree indexes.
/// </summary>
public class UrlMappingConfiguration : IEntityTypeConfiguration<UrlMapping>
{
    public void Configure(EntityTypeBuilder<UrlMapping> builder)
    {
        builder.ToTable("UrlMappings");

        // Primary key using PostgreSQL identity column (BIGINT / int64)
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();

        // ShortCode: Base62 string, max 16 chars, indexed for fast O(log N) lookup
        builder.Property(u => u.ShortCode)
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(u => u.ShortCode)
            .IsUnique()
            .HasDatabaseName("IX_UrlMappings_ShortCode");

        // OriginalUrl: URL text up to 2048 chars
        builder.Property(u => u.OriginalUrl)
            .HasMaxLength(2048)
            .IsRequired();

        // Timestamps and click analytics
        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.ExpiresAt)
            .IsRequired(false);

        builder.Property(u => u.ClickCount)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        // Composite index for querying active, non-expired URLs
        builder.HasIndex(u => new { u.ShortCode, u.IsActive })
            .HasDatabaseName("IX_UrlMappings_ShortCode_IsActive");
    }
}
