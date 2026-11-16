using System.Threading.Channels;
using ShortLink.Application.Common.Interfaces;

namespace ShortLink.Infrastructure.Services;

/// <summary>
/// High-performance thread-safe in-memory queue backed by System.Threading.Channels.
/// Guarantees sub-microsecond non-blocking enqueue for the HTTP 302 redirect hot path.
/// Includes backpressure protection with BoundedChannelFullMode.DropOldest.
/// </summary>
public class ClickAnalyticsChannel : IClickAnalyticsQueue
{
    private readonly Channel<string> _channel;
    private const int ChannelCapacity = 50_000;

    public ClickAnalyticsChannel()
    {
        var options = new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<string>(options);
    }

    public ValueTask EnqueueClickAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryWrite(shortCode);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
