using FsCheck.Xunit;
using VibeCraft.Primitives.Coordinates;
using Xunit;

namespace VibeCraft.G1.Tests.Coordinates;

public sealed class CoordinateTests
{
    public static TheoryData<int> SectionSides => [16, 32];

    [Theory]
    [MemberData(nameof(SectionSides))]
    public void DecomposeUsesMathematicalFloorDivisionAtNegativeBoundaries(int side)
    {
        SectionGeometry geometry = GeometryFor(side);
        AssertDecomposition(geometry, -1, -1, side - 1);
        AssertDecomposition(geometry, -side, -1, 0);
        AssertDecomposition(geometry, -side - 1, -2, side - 1);
    }

    [Theory]
    [MemberData(nameof(SectionSides))]
    public void DecomposeAndRecomposeSupportsCoordinatesBeyondInt32(int side)
    {
        SectionGeometry geometry = GeometryFor(side);
        BlockCoord block = new(
            (((long)int.MaxValue + 1) * side) + (side - 1),
            ((long)int.MinValue * side) - 1,
            ((long)int.MaxValue * side) + 1);

        SectionLocation location = geometry.Decompose(block);

        Assert.Equal(block, geometry.ToBlockCoord(location.Section, location.Local));
        Assert.True(location.Section.X > int.MaxValue);
        Assert.True(location.Section.Y < int.MinValue);
    }

    [Theory]
    [InlineData(16, -576460752303423488L, 576460752303423487L)]
    [InlineData(32, -288230376151711744L, 288230376151711743L)]
    public void LongExtremesDecomposeAndRoundTripSafely(int side, long minimumSectionCoordinate, long maximumSectionCoordinate)
    {
        SectionGeometry geometry = GeometryFor(side);
        BlockCoord minimum = new(long.MinValue, long.MinValue, long.MinValue);
        BlockCoord maximum = new(long.MaxValue, long.MaxValue, long.MaxValue);

        Assert.Equal(minimum, Recompose(geometry, minimum));
        Assert.Equal(maximum, Recompose(geometry, maximum));

        SectionCoord lowestSection = new(minimumSectionCoordinate, minimumSectionCoordinate, minimumSectionCoordinate);
        SectionCoord highestSection = new(maximumSectionCoordinate, maximumSectionCoordinate, maximumSectionCoordinate);
        SectionCoord belowMinimumSection = new(minimumSectionCoordinate - 1, 0, 0);
        SectionCoord aboveMaximumSection = new(maximumSectionCoordinate + 1, 0, 0);

        Assert.Equal(new BlockCoord(long.MinValue, long.MinValue, long.MinValue), geometry.GetOrigin(lowestSection));
        Assert.Equal(new BlockCoord(long.MinValue + side - 1, long.MinValue + side - 1, long.MinValue + side - 1), geometry.GetEndInclusive(lowestSection));
        Assert.Equal(new BlockCoord(long.MaxValue - (side - 1), long.MaxValue - (side - 1), long.MaxValue - (side - 1)), geometry.GetOrigin(highestSection));
        Assert.Equal(new BlockCoord(long.MaxValue, long.MaxValue, long.MaxValue), geometry.GetEndInclusive(highestSection));
        _ = Assert.Throws<OverflowException>(() => geometry.GetOrigin(belowMinimumSection));
        _ = Assert.Throws<OverflowException>(() => geometry.GetEndInclusive(belowMinimumSection));
        _ = Assert.Throws<OverflowException>(() => geometry.GetOrigin(aboveMaximumSection));
        _ = Assert.Throws<OverflowException>(() => geometry.GetEndInclusive(aboveMaximumSection));
    }

    [Theory]
    [MemberData(nameof(SectionSides))]
    public void OriginsEndsAndIndicesUseTheSelectedGeometry(int side)
    {
        SectionGeometry geometry = GeometryFor(side);
        SectionCoord section = new(-2, 3, -4);
        LocalBlock local = geometry.CreateLocal(side - 1, side - 1, side - 1);

        Assert.Equal(new BlockCoord(-2L * side, 3L * side, -4L * side), section.Origin(geometry));
        Assert.Equal(new BlockCoord(-side - 1, (4L * side) - 1, (-3L * side) - 1), section.EndInclusive(geometry));
        Assert.Equal(new LocalIndex((side * side * side) - 1), geometry.GetLocalIndex(local));
        Assert.Equal(new BlockCoord((-2L * side) + side - 1, (3L * side) + side - 1, (-4L * side) + side - 1), geometry.ToBlockCoord(section, local));
    }

    [Theory]
    [MemberData(nameof(SectionSides))]
    public void LocalIndicesUseExactXThenZThenYOrdering(int side)
    {
        SectionGeometry geometry = GeometryFor(side);

        Assert.Equal(new LocalIndex(1), geometry.GetLocalIndex(geometry.CreateLocal(1, 0, 0)));
        Assert.Equal(new LocalIndex(side), geometry.GetLocalIndex(geometry.CreateLocal(0, 0, 1)));
        Assert.Equal(new LocalIndex(side * side), geometry.GetLocalIndex(geometry.CreateLocal(0, 1, 0)));
    }

    [Theory]
    [MemberData(nameof(SectionSides))]
    public void EveryLocalPositionHasAUniqueInRangeRoundTripIndex(int side)
    {
        SectionGeometry geometry = GeometryFor(side);
        int sectionVolume = side * side * side;
        bool[] seenIndices = new bool[sectionVolume];

        for (int y = 0; y < side; y++)
        {
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    LocalBlock local = geometry.CreateLocal(x, y, z);
                    LocalIndex index = geometry.GetLocalIndex(local);

                    Assert.InRange(index.Value, 0, sectionVolume - 1);
                    Assert.False(seenIndices[index.Value]);
                    seenIndices[index.Value] = true;
                    Assert.Equal(local, geometry.GetLocalBlock(index));
                }
            }
        }

        Assert.All(seenIndices, Assert.True);
    }

    [Fact]
    public void NeighborAndOffsetOperationsAreChecked()
    {
        _ = Assert.Throws<OverflowException>(() => new SectionCoord(long.MaxValue, 0, 0).Neighbor(CoordinateDirection.PositiveX));
        _ = Assert.Throws<OverflowException>(() => new SectionCoord(long.MinValue, 0, 0).Neighbor(CoordinateDirection.NegativeX));
        _ = Assert.Throws<OverflowException>(() => new SectionCoord(0, long.MaxValue, 0).Neighbor(CoordinateDirection.PositiveY));
        _ = Assert.Throws<OverflowException>(() => new SectionCoord(0, long.MinValue, 0).Neighbor(CoordinateDirection.NegativeY));
        _ = Assert.Throws<OverflowException>(() => new SectionCoord(0, 0, long.MaxValue).Neighbor(CoordinateDirection.PositiveZ));
        _ = Assert.Throws<OverflowException>(() => new SectionCoord(0, 0, long.MinValue).Neighbor(CoordinateDirection.NegativeZ));
        _ = Assert.Throws<OverflowException>(() => new BlockCoord(long.MaxValue, 0, 0).Neighbor(CoordinateDirection.PositiveX));
        _ = Assert.Throws<OverflowException>(() => new ColumnCoord(long.MaxValue, 0).Offset(1, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SectionCoord(0, 0, 0).Neighbor((CoordinateDirection)99));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(33)]
    public void SectionSideRejectsValuesOutsideTheEvaluatedCandidates(int side)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SectionSide(side));
    }

    [Fact]
    public void LocalValuesAreCheckedAgainstByteAndGeometryDomains()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LocalBlock(-1, 0, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LocalBlock(0, 256, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SectionGeometry.Side16.CreateLocal(16, 0, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SectionGeometry.Side16.ToBlockCoord(new SectionCoord(0, 0, 0), new LocalBlock(16, 0, 0)));
        Assert.Equal(new LocalBlock(31, 0, 0), SectionGeometry.Side32.CreateLocal(31, 0, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => default(SectionGeometry).Decompose(new BlockCoord(0, 0, 0)));
    }

    [Theory]
    [MemberData(nameof(SectionSides))]
    public void LocalIndexRejectsNegativeAndGeometryOutOfRangeValues(int side)
    {
        SectionGeometry geometry = GeometryFor(side);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LocalIndex(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => geometry.GetLocalBlock(new LocalIndex(geometry.Volume)));
        Assert.Equal(0, geometry.GetLocalIndex(default).Value);
        Assert.Equal(default, geometry.GetLocalBlock(default));
    }

    [Fact]
    public void LocalIndexHasNumericOrderingWithoutIntegerConversions()
    {
        LocalIndex first = new(1);
        LocalIndex second = new(2);

        Assert.True(first < second);
        Assert.True(first <= second);
        Assert.True(second > first);
        Assert.True(second >= first);
        Assert.Equal(-1, Math.Sign(first.CompareTo(second)));
        Assert.DoesNotContain(
            typeof(LocalIndex).GetMethods(),
            method => method.Name is "op_Implicit" or "op_Explicit");
    }

    [Fact]
    public void DimensionRangesUseExplicitInclusiveMinimumAndExclusiveMaximum()
    {
        BlockYRange generation = new(-64, 320);
        DimensionRangePolicy policy = DimensionRangePolicy.CreateInitial(generation, -5_000);

        Assert.True(generation.IsValid);
        Assert.True(policy.BuildRange.IsValid);
        Assert.Equal(-5_000, policy.BuildRange.MinY);
        Assert.Equal(5_000, policy.BuildRange.MaxYExclusive);
        Assert.True(policy.BuildRange.Contains(-5_000));
        Assert.False(policy.BuildRange.Contains(5_000));
        Assert.True(policy.BuildRange.Contains(generation));
        Assert.Equal(DimensionRangePolicy.InitialBuildHeight, policy.BuildRange.MaxYExclusive - policy.BuildRange.MinY);
    }

    [Fact]
    public void InvalidRangesAndInitialRangeOverflowAreRejected()
    {
        BlockYRange validRange = new(-1, 1);
        BlockYRange invalidRange = default;

        Assert.False(invalidRange.IsValid);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BlockYRange(4, 4));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BlockYRange(4, 3));
        _ = Assert.Throws<InvalidOperationException>(() => invalidRange.Contains(0));
        _ = Assert.Throws<InvalidOperationException>(() => invalidRange.Contains(validRange));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => validRange.Contains(invalidRange));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DimensionRangePolicy(default, default));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DimensionRangePolicy(default, validRange));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DimensionRangePolicy(validRange, default));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => DimensionRangePolicy.CreateInitial(default, -5_000));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DimensionRangePolicy(new BlockYRange(-1, 11), new BlockYRange(0, 10)));
        _ = Assert.Throws<OverflowException>(() => DimensionRangePolicy.CreateInitial(new BlockYRange(long.MaxValue - 9_999, long.MaxValue), long.MaxValue - 9_999));
    }

    [Property(MaxTest = 1_000)]
    public bool EveryBlockCoordinateRoundTripsThroughBothCandidateGeometries(long x, long y, long z)
    {
        BlockCoord block = new(x, y, z);

        return Recompose(SectionGeometry.Side16, block) == block
            && Recompose(SectionGeometry.Side32, block) == block;
    }

    private static BlockCoord Recompose(SectionGeometry geometry, BlockCoord block)
    {
        SectionLocation location = geometry.Decompose(block);
        return geometry.ToBlockCoord(location.Section, location.Local);
    }

    private static SectionGeometry GeometryFor(int side)
    {
        return side switch
        {
            16 => SectionGeometry.Side16,
            32 => SectionGeometry.Side32,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "A supported test geometry is required."),
        };
    }

    private static void AssertDecomposition(SectionGeometry geometry, int coordinate, long expectedSection, int expectedLocal)
    {
        SectionLocation location = geometry.Decompose(new BlockCoord(coordinate, coordinate, coordinate));

        Assert.Equal(new SectionCoord(expectedSection, expectedSection, expectedSection), location.Section);
        Assert.Equal(new LocalBlock(expectedLocal, expectedLocal, expectedLocal), location.Local);
        Assert.Equal(new BlockCoord(coordinate, coordinate, coordinate), geometry.ToBlockCoord(location.Section, location.Local));
    }
}
