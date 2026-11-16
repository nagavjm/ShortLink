using ShortLink.Application.Common.Interfaces;

namespace ShortLink.Infrastructure.Services;

/// <summary>
/// Thread-safe Twitter Snowflake distributed 64-bit ID generator.
/// Layout:
/// - 1 bit  : Unused sign bit (0)
/// - 41 bits: Timestamp in milliseconds (custom epoch: 2024-01-01 UTC, supports ~69 years)
/// - 10 bits: Machine/Node ID (0 to 1023)
/// - 12 bits: Sequence number (0 to 4095 per millisecond per node)
/// </summary>
public class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    // Custom Epoch: 2024-01-01T00:00:00Z in Unix Epoch Milliseconds
    private const long CustomEpoch = 1704067200000L;

    private const int SequenceBits = 12;
    private const int MachineIdBits = 10;

    private const long MaxSequence = (1L << SequenceBits) - 1L; // 4095
    private const long MaxMachineId = (1L << MachineIdBits) - 1L; // 1023

    private const int MachineIdShift = SequenceBits; // 12
    private const int TimestampLeftShift = SequenceBits + MachineIdBits; // 22

    private readonly long _machineId;
    private readonly object _lock = new();

    private long _lastTimestamp = -1L;
    private long _sequence = 0L;

    public SnowflakeIdGenerator(long machineId = 1)
    {
        if (machineId < 0 || machineId > MaxMachineId)
        {
            throw new ArgumentOutOfRangeException(nameof(machineId), $"MachineId must be between 0 and {MaxMachineId}.");
        }

        _machineId = machineId;
    }

    public long NextId()
    {
        lock (_lock)
        {
            var currentTimestamp = GetCurrentTimestamp();

            if (currentTimestamp < _lastTimestamp)
            {
                // Clock drift / NTP backward adjustment detected
                var driftMs = _lastTimestamp - currentTimestamp;
                throw new InvalidOperationException($"Clock moved backwards by {driftMs}ms! Refusing to generate ID to avoid duplicates.");
            }

            if (currentTimestamp == _lastTimestamp)
            {
                // Same millisecond: increment sequence
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                {
                    // Sequence exhausted for this millisecond (over 4096 IDs in 1ms) - wait for next millisecond
                    currentTimestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                // New millisecond: reset sequence
                _sequence = 0L;
            }

            _lastTimestamp = currentTimestamp;

            return ((currentTimestamp - CustomEpoch) << TimestampLeftShift)
                 | (_machineId << MachineIdShift)
                 | _sequence;
        }
    }

    private static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }
}
