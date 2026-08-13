using FsCheck.Xunit;
using VibeCraft.Content;
using Xunit;

namespace VibeCraft.G1.Tests.Content;

public sealed class NamespacedContentIdTests
{
    [Theory]
    [InlineData("vibecraft:air")]
    [InlineData("mod_2:blocks/oak-log.01")]
    [InlineData("a:b")]
    public void ParseAcceptsCanonicalLowercaseAsciiGrammar(string value)
    {
        NamespacedContentId contentId = NamespacedContentId.Parse(value);

        Assert.Equal(value, contentId.ToString());
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
        Assert.False(NamespacedContentId.TryParse(value, out _));
        _ = Assert.Throws<FormatException>(() => NamespacedContentId.Parse(value));
    }

    [Property(MaxTest = 100)]
    public void ParsedCanonicalIdentifiersRoundTripWithOrdinalSemantics(uint value)
    {
        string @namespace = $"mod_{value % 997}";
        string path = $"blocks/path-{value % 65_537}";
        NamespacedContentId contentId = NamespacedContentId.Create(@namespace, path);

        Assert.True(NamespacedContentId.TryParse(contentId.ToString(), out NamespacedContentId reparsed));
        Assert.Equal(contentId, reparsed);
        Assert.Equal(0, contentId.CompareTo(reparsed));
    }

    [Fact]
    public void IdentifierComparisonIsOrdinalRatherThanCultureSensitive()
    {
        NamespacedContentId first = NamespacedContentId.Parse("vibecraft:alpha");
        NamespacedContentId second = NamespacedContentId.Parse("vibecraft:beta");

        Assert.True(first.CompareTo(second) < 0);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(
            typeof(NamespacedContentId).GetMethods(),
            method => method.Name is "op_Implicit" or "op_Explicit");
    }

    [Fact]
    public void ComponentsAreLengthBoundedWithoutNormalizationOrTruncation()
    {
        string maximumNamespace = new('a', NamespacedContentId.MaxNamespaceLength);
        string maximumPath = new('b', NamespacedContentId.MaxPathLength);

        Assert.Equal(
            $"{maximumNamespace}:{maximumPath}",
            NamespacedContentId.Create(maximumNamespace, maximumPath).ToString());
        _ = Assert.Throws<ArgumentException>(() => NamespacedContentId.Create($"{maximumNamespace}a", "path"));
        _ = Assert.Throws<ArgumentException>(() => NamespacedContentId.Create("namespace", $"{maximumPath}b"));
    }

    [Fact]
    public void DefaultContentIdIsExplicitlyInvalidAndRejectedAtPublicConstructionBoundaries()
    {
        NamespacedContentId invalid = default;

        Assert.False(invalid.IsValid);
        Assert.Equal("<invalid-namespaced-content-id>", invalid.ToString());
        _ = Assert.Throws<InvalidOperationException>(() => invalid.CompareTo(NamespacedContentId.Parse("vibecraft:air")));
        _ = Assert.Throws<InvalidOperationException>(() => BlockStateProperty.Create(invalid, "north"));
        _ = Assert.Throws<InvalidOperationException>(() => new CanonicalBlockState(invalid, []));
        _ = Assert.Throws<InvalidOperationException>(() => ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            invalid,
            "definition"));
    }
}
