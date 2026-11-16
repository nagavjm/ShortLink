namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// High-throughput in-memory queue contract for asynchronous click tracking.
/// Decouples the redirect hot path from database write latency.
/// Extensible for seamless replacement with Azure Service Bus, RabbitMQ, or Kafka.
/// </summary>
public interface IClickAnalyticsQueue
{
    /// <summary>
    /// Enqueues a click event for asynchronous background processing.
    /// Non-blocking, executes in sub-microsecond time.
    /// </summary>
    ValueTask EnqueueClickAsync(string shortCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads queued click events as an asynchronous stream.
    /// Consumed by background workers.
    /// </summary>
    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken = default);
}
