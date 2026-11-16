using ShortLink.Infrastructure.Services;
using Xunit;

namespace ShortLink.UnitTests;

public class Base62ServiceTests
{
    private readonly Base62Service _sut = new();

    [Fact]
    public void Encode_Zero_ReturnsZeroCharacter()
    {
        var result = _sut.Encode(0);
        Assert.Equal("0", result);
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(10, "a")]
    [InlineData(35, "z")]
    [InlineData(36, "A")]
    [InlineData(61, "Z")]
    [InlineData(62, "10")]
    [InlineData(3843, "ZZ")]
    [InlineData(3844, "100")]
    [InlineData(12530, "3g6")]
    public void Encode_KnownNumbers_ReturnsExpectedBase62String(long number, string expected)
    {
        var result = _sut.Encode(number);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Encode_NegativeNumber_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Encode(-1));
    }

    [Theory]
    [InlineData(1, 6, "000001")]
    [InlineData(62, 6, "000010")]
    [InlineData(12530, 6, "0003g6")]
    [InlineData(14776336, 6, "010000")]
    public void EncodeWithPadding_PadsToExpectedMinimumLength(long number, int minLength, string expected)
    {
        var result = _sut.EncodeWithPadding(number, minLength);
        Assert.Equal(expected, result);
        Assert.Equal(minLength, result.Length);
    }

    [Fact]
    public void Decode_KnownBase62Strings_ReturnsOriginalNumber()
    {
        Assert.Equal(0, _sut.Decode("0"));
        Assert.Equal(1, _sut.Decode("1"));
        Assert.Equal(10, _sut.Decode("a"));
        Assert.Equal(35, _sut.Decode("z"));
        Assert.Equal(36, _sut.Decode("A"));
        Assert.Equal(61, _sut.Decode("Z"));
        Assert.Equal(62, _sut.Decode("10"));
        Assert.Equal(3843, _sut.Decode("ZZ"));
        Assert.Equal(3844, _sut.Decode("100"));
    }

    [Fact]
    public void Roundtrip_EncodeAndDecode_MaintainsIntegrityAcrossThousands()
    {
        for (long i = 0; i < 10000; i++)
        {
            var encoded = _sut.Encode(i);
            var decoded = _sut.Decode(encoded);
            Assert.Equal(i, decoded);
        }
    }

    [Fact]
    public void Roundtrip_Large64BitIntegers_MaintainsIntegrity()
    {
        long[] testValues =
        {
            100_000_000L,
            1_000_000_000L,
            9_223_372_036_854_775_800L, // Near long.MaxValue
            long.MaxValue
        };

        foreach (var val in testValues)
        {
            var encoded = _sut.Encode(val);
            var decoded = _sut.Decode(encoded);
            Assert.Equal(val, decoded);
        }
    }

    [Theory]
    [InlineData("!invalid")]
    [InlineData("abc-123")]
    [InlineData("hello world")]
    [InlineData("test#1")]
    public void Decode_InvalidCharacters_ThrowsFormatException(string invalidInput)
    {
        Assert.Throws<FormatException>(() => _sut.Decode(invalidInput));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decode_NullOrWhitespace_ThrowsArgumentException(string? invalidInput)
    {
        Assert.Throws<ArgumentException>(() => _sut.Decode(invalidInput!));
    }

    [Theory]
    [InlineData("aB92xK", true)]
    [InlineData("000001", true)]
    [InlineData("ZZZZ", true)]
    [InlineData("aB92_K", false)]
    [InlineData("aB92.K", false)]
    [InlineData("", false)]
    public void IsValidBase62_ValidatesCharacterCorrectness(string input, bool expected)
    {
        var isValid = _sut.IsValidBase62(input);
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void Uniqueness_ZeroCollisionsAcrossLargeBatch()
    {
        var generatedCodes = new HashSet<string>(50000);

        for (long i = 1; i <= 50000; i++)
        {
            var code = _sut.Encode(i);
            var added = generatedCodes.Add(code);
            Assert.True(added, $"Collision detected for number {i} generating code {code}");
        }

        Assert.Equal(50000, generatedCodes.Count);
    }
}
