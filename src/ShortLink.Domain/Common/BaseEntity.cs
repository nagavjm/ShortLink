namespace ShortLink.Domain.Common;

/// <summary>
/// Base class for all domain entities providing a strongly-typed primary key.
/// Using long (Int64) for high-scale distributed ID compatibility (Snowflake/PostgreSQL BIGINT).
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; protected set; }
}
