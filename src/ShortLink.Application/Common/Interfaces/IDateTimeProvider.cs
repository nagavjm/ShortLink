namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Abstraction for system clock to ensure deterministic testability and UTC standardization.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
