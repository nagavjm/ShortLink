namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Strategy contract for generating unique short codes.
/// Allows swapping between PostgreSQL Auto-Increment + Base62 and Distributed Snowflake generators.
/// </summary>
public interface IShortCodeGenerator
{
    /// <summary>
    /// Generates a unique short code from an entity ID.
    /// </summary>
    string Generate(long entityId);

    /// <summary>
    /// Strategy name for telemetry and diagnostics.
    /// </summary>
    string StrategyName { get; }
}
