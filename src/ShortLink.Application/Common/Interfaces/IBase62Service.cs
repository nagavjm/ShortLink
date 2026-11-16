namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Service abstraction for Base62 encoding and decoding of 64-bit numeric IDs.
/// Uses character set [0-9, a-z, A-Z] (62 alphanumeric characters).
/// </summary>
public interface IBase62Service
{
    /// <summary>
    /// Encodes a 64-bit integer into a Base62 string.
    /// </summary>
    string Encode(long number);

    /// <summary>
    /// Encodes a 64-bit integer into a Base62 string with minimum character padding (default 6).
    /// </summary>
    string EncodeWithPadding(long number, int minLength = 6);

    /// <summary>
    /// Decodes a Base62 string back into a 64-bit integer.
    /// Throws FormatException if the input contains invalid Base62 characters.
    /// </summary>
    long Decode(string base62String);

    /// <summary>
    /// Attempts to decode a Base62 string without throwing exceptions.
    /// </summary>
    bool TryDecode(string base62String, out long result);

    /// <summary>
    /// Validates whether a string consists entirely of valid Base62 characters.
    /// </summary>
    bool IsValidBase62(string value);
}
