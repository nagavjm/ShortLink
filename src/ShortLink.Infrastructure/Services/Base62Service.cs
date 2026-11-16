using System.Text;
using ShortLink.Application.Common.Interfaces;

namespace ShortLink.Infrastructure.Services;

/// <summary>
/// Production-grade Base62 encoding and decoding service.
/// Alphabet: 0-9 (10) + a-z (26) + A-Z (26) = 62 characters.
/// Mathematically bijective mapping between 64-bit non-negative integers and alphanumeric strings.
/// </summary>
public class Base62Service : IBase62Service
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly int Base = Alphabet.Length; // 62
    private static readonly int[] CharToValueLookup;

    static Base62Service()
    {
        // 128-byte lookup array for ASCII O(1) character-to-value resolution
        CharToValueLookup = new int[128];
        Array.Fill(CharToValueLookup, -1);

        for (int i = 0; i < Alphabet.Length; i++)
        {
            char c = Alphabet[i];
            CharToValueLookup[c] = i;
        }
    }

    public string Encode(long number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Base62 encoding requires a non-negative 64-bit integer.");
        }

        if (number == 0)
        {
            return Alphabet[0].ToString();
        }

        var sb = new StringBuilder(12);
        var current = number;

        while (current > 0)
        {
            var remainder = (int)(current % Base);
            sb.Append(Alphabet[remainder]);
            current /= Base;
        }

        // Positional notation: reverse remainder list to place most-significant character on the left
        var chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    public string EncodeWithPadding(long number, int minLength = 6)
    {
        var rawEncoded = Encode(number);
        if (rawEncoded.Length >= minLength)
        {
            return rawEncoded;
        }

        return rawEncoded.PadLeft(minLength, Alphabet[0]);
    }

    public long Decode(string base62String)
    {
        if (string.IsNullOrWhiteSpace(base62String))
        {
            throw new ArgumentException("Input string cannot be null or empty.", nameof(base62String));
        }

        long result = 0;

        foreach (char c in base62String)
        {
            if (c >= 128 || CharToValueLookup[c] == -1)
            {
                throw new FormatException($"Character '{c}' is not a valid Base62 character [0-9, a-z, A-Z].");
            }

            int val = CharToValueLookup[c];

            // checked keyword prevents silent 64-bit integer overflow
            checked
            {
                result = (result * Base) + val;
            }
        }

        return result;
    }

    public bool TryDecode(string base62String, out long result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(base62String))
        {
            return false;
        }

        long acc = 0;
        foreach (char c in base62String)
        {
            if (c >= 128 || CharToValueLookup[c] == -1)
            {
                return false;
            }

            int val = CharToValueLookup[c];
            try
            {
                checked
                {
                    acc = (acc * Base) + val;
                }
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        result = acc;
        return true;
    }

    public bool IsValidBase62(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (char c in value)
        {
            if (c >= 128 || CharToValueLookup[c] == -1)
            {
                return false;
            }
        }

        return true;
    }
}
