using ShortLink.Application.Common.Interfaces;

namespace ShortLink.Infrastructure.Services;

/// <summary>
/// Short code generator strategy using PostgreSQL Auto-Increment 64-bit ID + Base62.
/// Pads with leading zeros if length is less than 6 to ensure clean, consistent short links.
/// </summary>
public class AutoIncrementShortCodeGenerator : IShortCodeGenerator
{
    private readonly IBase62Service _base62Service;
    private const int MinimumCodeLength = 6;

    public AutoIncrementShortCodeGenerator(IBase62Service base62Service)
    {
        _base62Service = base62Service;
    }

    public string StrategyName => "PostgreSql-AutoIncrement-Base62";

    public string Generate(long entityId)
    {
        return _base62Service.EncodeWithPadding(entityId, MinimumCodeLength);
    }
}
