using FsCheck.Xunit;
using VibeCraft.Content;
using Xunit;

namespace VibeCraft.G1.Tests.Content;

public sealed class ContentKeyTests
{
    [Theory]
    [InlineData("vibecraft:air")]
    [InlineData("mod_2:blocks/oak-log.01")]
    [InlineData("a:b")]
    public void ParseAcceptsCanonicalLowercaseAsciiGrammar(string value)
    {
        ContentKey key = ContentKey.Parse(value);

        Assert.Equal(value, key.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("vibecraft")]
    [InlineData(":air")]
    [InlineData("vibecraft:")]
    [InlineData("VibeCraft:air")]
    [InlineData("vibecraft:Air")]
    [InlineData("vibecraft:öak")]
    [InlineData("vibecraft:air:extra")]
    [InlineData("vibe craft:air")]
    public void ParseRejectsNonCanonicalGrammar(string value)
    {
        Assert.False(ContentKey.TryParse(value, out _));
        _ = Assert.Throws<FormatException>(() => ContentKey.Parse(value));
    }

    [Property(MaxTest = 100)]
    public void ParsedCanonicalKeysRoundTripWithOrdinalSemantics(uint value)
    {
        string @namespace = $"mod_{value % 997}";
        string path = $"blocks/path-{value % 65_537}";
        ContentKey key = ContentKey.Create(@namespace, path);

        Assert.True(ContentKey.TryParse(key.ToString(), out ContentKey reparsed));
        Assert.Equal(key, reparsed);
        Assert.Equal(0, key.CompareTo(reparsed));
    }

    [Fact]
    public void KeyComparisonIsOrdinalRatherThanCultureSensitive()
    {
        ContentKey first = ContentKey.Parse("vibecraft:alpha");
        ContentKey second = ContentKey.Parse("vibecraft:beta");

        Assert.True(first.CompareTo(second) < 0);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DefaultKeyIsExplicitlyInvalidAndRejectedAtPublicConstructionBoundaries()
    {
        ContentKey invalid = default;

        Assert.False(invalid.IsValid);
        Assert.Equal("<invalid-content-key>", invalid.ToString());
        _ = Assert.Throws<InvalidOperationException>(() => invalid.CompareTo(ContentKey.Parse("vibecraft:air")));
        _ = Assert.Throws<InvalidOperationException>(() => BlockStateProperty.Create(invalid, "north"));
        _ = Assert.Throws<InvalidOperationException>(() => new CanonicalBlockState(invalid, []));
        _ = Assert.Throws<InvalidOperationException>(() => ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            invalid,
            "definition"));
    }
}
