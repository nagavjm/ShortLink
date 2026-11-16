namespace ShortLink.Application.Common.Interfaces;

/// <summary>
/// Service abstraction for Base62 encoding and decoding of numeric IDs.
/// Uses characters [0-9, a-z, A-Z].
/// </summary>
public interface IBase62Service
{
    /// <summary>
    /// Encodes a 64-bit integer into a Base62 string.
    /// </summary>
    string Encode(long number);

    /// <summary>
    /// Decodes a Base62 string back into a 64-bit integer.
    /// </summary>
    long Decode(string base62String);
}
