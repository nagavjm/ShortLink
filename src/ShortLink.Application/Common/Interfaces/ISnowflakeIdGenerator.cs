namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Contract for distributed 64-bit Snowflake ID generation.
/// Bit breakdown: 1 bit unused, 41 bits timestamp, 10 bits machine/datacenter ID, 12 bits sequence.
/// Produces up to 4,096 IDs per millisecond per node without database locks.
/// </summary>
public interface ISnowflakeIdGenerator
{
    long NextId();
}
