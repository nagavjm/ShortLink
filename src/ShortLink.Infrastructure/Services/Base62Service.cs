using System.Text;
using ShortLink.Application.Common.Interfaces;

namespace ShortLink.Infrastructure.Services;

/// <summary>
/// Production-grade Base62 encoding and decoding service.
/// Alphabet: 0-9 (10) + a-z (26) + A-Z (26) = 62 characters.
/// Supports 64-bit unsigned/signed integers compatible with PostgreSQL BIGINT and Twitter Snowflake.
/// </summary>
public class Base62Service : IBase62Service
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly int Base = Alphabet.Length; // 62
    private static readonly Dictionary<char, int> CharToValueMap;

    static Base62Service()
    {
        CharToValueMap = new Dictionary<char, int>(Base);
        for (int i = 0; i < Base; i++)
        {
            CharToValueMap[Alphabet[i]] = i;
        }
    }

    /// <summary>
    /// Encodes a positive 64-bit integer to a Base62 string.
    /// </summary>
    public string Encode(long number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Base62 encoding requires a non-negative number.");
        }

        if (number == 0)
        {
            return Alphabet[0].ToString();
        }

        var sb = new StringBuilder();
        var current = number;

        while (current > 0)
        {
            var remainder = (int)(current % Base);
            sb.Append(Alphabet[remainder]);
            current /= Base;
        }

        // Reverse to get most-significant character first (standard big-endian positional notation)
        var chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// Decodes a Base62 string back to a 64-bit integer.
    /// </summary>
    public long Decode(string base62String)
    {
        if (string.IsNullOrWhiteSpace(base62String))
        {
            throw new ArgumentException("Input string cannot be null or empty.", nameof(base62String));
        }

        long result = 0;

        foreach (var c in base62String)
        {
            if (!CharToValueMap.TryGetValue(c, out var val))
            {
                throw new FormatException($"Character '{c}' is not a valid Base62 character.");
            }

            // Check for potential 64-bit overflow
            checked
            {
                result = (result * Base) + val;
            }
        }

        return result;
    }
}
