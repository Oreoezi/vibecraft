using VibeCraft.Content;
using Xunit;

namespace VibeCraft.G1.Tests.Content;

public sealed class BlockStateIdentityTests
{
    [Fact]
    public void StatePropertiesAreSortedByOrdinalContentKey()
    {
        CanonicalBlockState state = new(
            ContentKey.Parse("vibecraft:oak_log"),
            [
                BlockStateProperty.Create(ContentKey.Parse("vibecraft:waterlogged"), "false"),
                BlockStateProperty.Create(ContentKey.Parse("vibecraft:axis"), "y"),
            ]);

        Assert.Equal(
            ["vibecraft:axis", "vibecraft:waterlogged"],
            state.Properties.Select(property => property.Key.ToString()));
        Assert.Equal("vibecraft:oak_log[vibecraft:axis=y,vibecraft:waterlogged=false]", state.ToString());
    }

    [Fact]
    public void DuplicatePropertiesAndVariantAirAreRejected()
    {
        ContentKey axis = ContentKey.Parse("vibecraft:axis");

        _ = Assert.Throws<ArgumentException>(() => new CanonicalBlockState(
            ContentKey.Parse("vibecraft:oak_log"),
            [BlockStateProperty.Create(axis, "x"), BlockStateProperty.Create(axis, "y")]));
        _ = Assert.Throws<ArgumentException>(() => new CanonicalBlockState(
            ContentKey.Parse("vibecraft:air"),
            [BlockStateProperty.Create(axis, "x")]));
    }

    [Fact]
    public void StateIdDomainsHaveNoImplicitConversionOperators()
    {
        Type[] domains = [typeof(WorldStateId), typeof(RuntimeStateId), typeof(SessionStateId)];

        foreach (Type domain in domains)
        {
            Assert.DoesNotContain(domain.GetMethods(), method => method.Name is "op_Implicit" or "op_Explicit");
        }

        Assert.NotEqual(typeof(WorldStateId), typeof(RuntimeStateId));
        Assert.NotEqual(typeof(RuntimeStateId), typeof(SessionStateId));
    }

    [Fact]
    public void DefaultPropertyIsInvalidAndRejectedByBlockStateConstruction()
    {
        BlockStateProperty invalid = default;

        Assert.False(invalid.IsValid);
        _ = Assert.Throws<ArgumentException>(() => new CanonicalBlockState(ContentKey.Parse("vibecraft:stone"), [invalid]));
    }

    [Fact]
    public void PropertyLimitStopsEnumerationAtPropertyThirtyThreeBeforeSorting()
    {
        int yielded = 0;
        ContentKey propertyKey = ContentKey.Parse("vibecraft:property");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CanonicalBlockState(
            ContentKey.Parse("vibecraft:bounded"),
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
