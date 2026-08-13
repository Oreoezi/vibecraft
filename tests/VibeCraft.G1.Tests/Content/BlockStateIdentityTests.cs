using VibeCraft.Content;
using Xunit;

namespace VibeCraft.G1.Tests.Content;

public sealed class BlockStateIdentityTests
{
    [Fact]
    public void StatePropertiesAreSortedByOrdinalNamespacedContentId()
    {
        CanonicalBlockState state = new(
            NamespacedContentId.Parse("vibecraft:oak_log"),
            [
                BlockStateProperty.Create(NamespacedContentId.Parse("vibecraft:waterlogged"), "false"),
                BlockStateProperty.Create(NamespacedContentId.Parse("vibecraft:axis"), "y"),
            ]);

        Assert.Equal(
            ["vibecraft:axis", "vibecraft:waterlogged"],
            state.Properties.Select(property => property.Key.ToString()));
        Assert.Equal("vibecraft:oak_log[vibecraft:axis=y,vibecraft:waterlogged=false]", state.ToString());
    }

    [Fact]
    public void DuplicatePropertiesAndVariantAirAreRejected()
    {
        NamespacedContentId axis = NamespacedContentId.Parse("vibecraft:axis");

        _ = Assert.Throws<ArgumentException>(() => new CanonicalBlockState(
            NamespacedContentId.Parse("vibecraft:oak_log"),
            [BlockStateProperty.Create(axis, "x"), BlockStateProperty.Create(axis, "y")]));
        _ = Assert.Throws<ArgumentException>(() => new CanonicalBlockState(
            NamespacedContentId.Parse("vibecraft:air"),
            [BlockStateProperty.Create(axis, "x")]));
    }

    [Fact]
    public void StateIdDomainsHaveNoImplicitConversionOperators()
    {
        Type[] domains = [typeof(BlockStateId), typeof(RuntimeStateId), typeof(SessionStateId)];

        foreach (Type domain in domains)
        {
            Assert.DoesNotContain(domain.GetMethods(), method => method.Name is "op_Implicit" or "op_Explicit");
        }

        Assert.NotEqual(typeof(BlockStateId), typeof(RuntimeStateId));
        Assert.NotEqual(typeof(RuntimeStateId), typeof(SessionStateId));
    }

    [Fact]
    public void BlockStateIdPreservesTheFullUnsignedDomain()
    {
        Assert.Equal(uint.MinValue, default(BlockStateId).Value);
        Assert.Equal(uint.MaxValue, new BlockStateId(uint.MaxValue).Value);
        Assert.NotEqual(default, new BlockStateId(uint.MaxValue));
    }

    [Fact]
    public void DefaultPropertyIsInvalidAndRejectedByBlockStateConstruction()
    {
        BlockStateProperty invalid = default;

        Assert.False(invalid.IsValid);
        _ = Assert.Throws<ArgumentException>(() => new CanonicalBlockState(NamespacedContentId.Parse("vibecraft:stone"), [invalid]));
    }

    [Fact]
    public void PropertyLimitStopsEnumerationAtPropertyThirtyThreeBeforeSorting()
    {
        int yielded = 0;
        NamespacedContentId propertyKey = NamespacedContentId.Parse("vibecraft:property");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CanonicalBlockState(
            NamespacedContentId.Parse("vibecraft:bounded"),
            InfiniteProperties()));
        Assert.Equal(CanonicalBlockState.MaxProperties + 1, yielded);

        IEnumerable<BlockStateProperty> InfiniteProperties()
        {
            while (true)
            {
                yielded++;
                yield return BlockStateProperty.Create(propertyKey, "value");
            }
        }
    }
}
