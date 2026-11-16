using ShortLink.Application.Common.Interfaces;

namespace ShortLink.Infrastructure.Services;

/// <summary>
/// Provides current system time in UTC.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
