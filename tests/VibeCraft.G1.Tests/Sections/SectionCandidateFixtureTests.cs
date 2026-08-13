using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.Sections;

public sealed class SectionCandidateFixtureTests
{
    [Fact]
    public void FixtureIdentityAndSeedAreExplicit()
    {
        Assert.Equal("VC-G1-E1-SECTIONS-0.1.0", SectionCandidateFixture.FixtureId);
        Assert.Equal(0x5643424654314531UL, SectionCandidateFixture.DefaultSeed);
    }

    [Theory]
    [InlineData(16, (int)SectionFixtureKind.UniformAir)]
    [InlineData(16, (int)SectionFixtureKind.UniformStone)]
    [InlineData(16, (int)SectionFixtureKind.Layered)]
    [InlineData(16, (int)SectionFixtureKind.Mixed)]
    [InlineData(16, (int)SectionFixtureKind.HighEntropy)]
    [InlineData(32, (int)SectionFixtureKind.UniformAir)]
    [InlineData(32, (int)SectionFixtureKind.UniformStone)]
    [InlineData(32, (int)SectionFixtureKind.Layered)]
    [InlineData(32, (int)SectionFixtureKind.Mixed)]
    [InlineData(32, (int)SectionFixtureKind.HighEntropy)]
    public void NamedFixturesAreDeterministic(int side, int kindValue)
    {
        SectionGeometry geometry = new(new SectionSide(side));
        SectionFixtureKind kind = (SectionFixtureKind)kindValue;
        WorldStateId[] first = SectionCandidateFixture.CreateStates(geometry, kind);
        WorldStateId[] second = SectionCandidateFixture.CreateStates(geometry, kind);

        Assert.Equal(first, second);
        Assert.Equal(side * side * side, first.Length);
    }

    [Fact]
    public void FixtureDefinitionsHaveExpectedSemanticShapes()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        WorldStateId[] air = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.UniformAir);
        WorldStateId[] stone = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.UniformStone);
        WorldStateId[] layered = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.Layered);
        WorldStateId[] entropy = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.HighEntropy);

        Assert.All(air, value => Assert.Equal(0U, value.Value));
        Assert.All(stone, value => Assert.Equal(1U, value.Value));
        Assert.Equal(new WorldStateId(1), layered[0]);
        Assert.Equal(new WorldStateId(2), layered[8 * 16 * 16]);
        Assert.Equal(new WorldStateId(3), layered[9 * 16 * 16]);
        Assert.Equal(new WorldStateId(0), layered[10 * 16 * 16]);
        Assert.Equal(Enumerable.Range(1, entropy.Length).Select(value => checked((uint)value)), entropy.Select(value => value.Value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(256)]
    [InlineData(257)]
    public void PaletteBoundaryFixtureContainsExactDistinctCount(int paletteSize)
    {
        WorldStateId[] states = SectionCandidateFixture.CreateStates(
            SectionGeometry.Side16,
            SectionFixtureKind.PaletteBoundary,
            paletteSize: paletteSize);

        Assert.Equal(paletteSize, states.Distinct().Count());
    }

    [Fact]
    public void EightSide16SectionsReconstructTheIdenticalCanonicalSide32Cube()
    {
        WorldStateId[] canonical = SectionCandidateFixture.CreateStates(SectionGeometry.Side32, SectionFixtureKind.Mixed);
        WorldStateId[] reconstructed = new WorldStateId[canonical.Length];

        for (int sectionY = 0; sectionY < 2; sectionY++)
        {
            for (int sectionZ = 0; sectionZ < 2; sectionZ++)
            {
                for (int sectionX = 0; sectionX < 2; sectionX++)
                {
                    WorldStateId[] octant = SectionCandidateFixture.ExtractSide16(canonical, sectionX, sectionY, sectionZ);
                    for (int y = 0; y < 16; y++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            for (int x = 0; x < 16; x++)
                            {
                                int source = x + (16 * (z + (16 * y)));
                                int globalX = (sectionX * 16) + x;
                                int globalY = (sectionY * 16) + y;
                                int globalZ = (sectionZ * 16) + z;
                                int destination = globalX + (32 * (globalZ + (32 * globalY)));
                                reconstructed[destination] = octant[source];
                            }
                        }
                    }
                }
            }
        }

        Assert.Equal(canonical, reconstructed);
    }

    [Fact]
    public void FixtureConstructionPreflightsRevisionHeadroomAndReturnsExactDenseState()
    {
        WorldStateId[] semantic = new WorldStateId[16 * 16 * 16];
        semantic[1] = new WorldStateId(4);
        semantic[50] = new WorldStateId(5);
        semantic[^1] = new WorldStateId(6);

        MutableSectionBlockStates section = SectionCandidateFixture.CreateSection(
            SectionGeometry.Side16,
            semantic,
            new(long.MaxValue - 3));
        WorldStateId[] actual = new WorldStateId[semantic.Length];
        section.CopyTo(actual);

        Assert.Equal(semantic, actual);
        Assert.Equal(long.MaxValue, section.Revision.Value);
    }

    [Fact]
    public void FixtureConstructionRefusesNearExhaustionBeforeProducingPartialState()
    {
        WorldStateId[] semantic = new WorldStateId[16 * 16 * 16];
        semantic[1] = new WorldStateId(4);
        semantic[50] = new WorldStateId(5);
        semantic[^1] = new WorldStateId(6);
        WorldStateId[] unchanged = (WorldStateId[])semantic.Clone();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SectionCandidateFixture.CreateSection(
            SectionGeometry.Side16,
            semantic,
            new(long.MaxValue - 2)));

        Assert.Equal(unchanged, semantic);
    }

    [Theory]
    [InlineData((int)SectionFixtureKind.UniformAir)]
    [InlineData((int)SectionFixtureKind.UniformStone)]
    [InlineData((int)SectionFixtureKind.Layered)]
    [InlineData((int)SectionFixtureKind.Mixed)]
    [InlineData((int)SectionFixtureKind.HighEntropy)]
    public void EqualVolumeGlobalPathsMatchCanonicalWorldForEveryFixture(int fixtureValue)
    {
        SectionFixtureKind fixture = (SectionFixtureKind)fixtureValue;
        WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(fixture);
        foreach (SectionEqualVolumeLayout layout in Enum.GetValues<SectionEqualVolumeLayout>())
        {
            MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
            for (int index = 0; index < canonical.Length; index++)
            {
                Assert.Equal(canonical[index], SectionEqualVolumeFixture.GetGlobal(sections, layout, index));
            }

            WorldStateId[] projection = new WorldStateId[canonical.Length];
            WorldStateId[][] scratch = CreateSide16Scratch();
            SectionEqualVolumeFixture.CopyToCanonical(sections, layout, projection, scratch);
            Assert.Equal(canonical, projection);
        }
    }

    [Fact]
    public void EqualVolumePathsRejectSectionsWithTheWrongGeometry()
    {
        MutableSectionBlockStates[] oneSide16 = [new(SectionGeometry.Side16, default, default)];
        MutableSectionBlockStates[] eightSide32 =
        [
            .. Enumerable.Range(0, 8)
                .Select(_ => new MutableSectionBlockStates(SectionGeometry.Side32, default, default)),
        ];
        WorldStateId[] projection = new WorldStateId[SectionEqualVolumeFixture.CubeVolume];

        _ = Assert.Throws<ArgumentException>(() => SectionEqualVolumeFixture.GetGlobal(
            oneSide16,
            SectionEqualVolumeLayout.OneSide32,
            0));
        _ = Assert.Throws<ArgumentException>(() => SectionEqualVolumeFixture.SetGlobal(
            eightSide32,
            SectionEqualVolumeLayout.EightSide16,
            new SectionEdit(0, new WorldStateId(1), SectionEditIntent.NewStateChange)));
        _ = Assert.Throws<ArgumentException>(() => SectionEqualVolumeFixture.CopyToCanonical(
            eightSide32,
            SectionEqualVolumeLayout.EightSide16,
            projection,
            CreateSide16Scratch()));
    }

    [Fact]
    public void EqualVolumeProjectionRejectsAliasedSide16ScratchArrays()
    {
        WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed);
        MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(
            SectionEqualVolumeLayout.EightSide16,
            canonical);
        WorldStateId[] shared = new WorldStateId[16 * 16 * 16];
        WorldStateId[][] aliasedScratch = [.. Enumerable.Repeat(shared, 8)];

        _ = Assert.Throws<ArgumentException>(() => SectionEqualVolumeFixture.CopyToCanonical(
            sections,
            SectionEqualVolumeLayout.EightSide16,
            new WorldStateId[canonical.Length],
            aliasedScratch));
    }

    [Theory]
    [InlineData((int)SectionFixtureKind.UniformAir, (int)SectionEditTraceKind.InteriorClusters)]
    [InlineData((int)SectionFixtureKind.UniformAir, (int)SectionEditTraceKind.BoundaryClusters)]
    [InlineData((int)SectionFixtureKind.UniformStone, (int)SectionEditTraceKind.InteriorClusters)]
    [InlineData((int)SectionFixtureKind.UniformStone, (int)SectionEditTraceKind.BoundaryClusters)]
    [InlineData((int)SectionFixtureKind.Layered, (int)SectionEditTraceKind.InteriorClusters)]
    [InlineData((int)SectionFixtureKind.Layered, (int)SectionEditTraceKind.BoundaryClusters)]
    [InlineData((int)SectionFixtureKind.Mixed, (int)SectionEditTraceKind.InteriorClusters)]
    [InlineData((int)SectionFixtureKind.Mixed, (int)SectionEditTraceKind.BoundaryClusters)]
    [InlineData((int)SectionFixtureKind.HighEntropy, (int)SectionEditTraceKind.InteriorClusters)]
    [InlineData((int)SectionFixtureKind.HighEntropy, (int)SectionEditTraceKind.BoundaryClusters)]
    public void EqualVolumeSetAndCopyRemainIdenticalAfterDeterministicTrace(int fixtureValue, int traceValue)
    {
        SectionFixtureKind fixture = (SectionFixtureKind)fixtureValue;
        SectionEditTraceKind traceKind = (SectionEditTraceKind)traceValue;
        WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(fixture);
        SectionEdit[] trace = SectionEqualVolumeFixture.CreateEditTrace(canonical, traceKind, clusterCount: 8);
        Assert.Contains(trace, edit => edit.Intent == SectionEditIntent.NoOp);
        Assert.Contains(trace, edit => edit.Intent == SectionEditIntent.NewStateChange);

        foreach (SectionEqualVolumeLayout layout in Enum.GetValues<SectionEqualVolumeLayout>())
        {
            WorldStateId[] dense = (WorldStateId[])canonical.Clone();
            MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
            foreach (SectionEdit edit in trace)
            {
                SectionWriteResult denseResult = SectionEqualVolumeFixture.SetDense(dense, edit);
                SectionWriteResult adaptiveResult = SectionEqualVolumeFixture.SetGlobal(sections, layout, edit);
                Assert.Equal(denseResult, adaptiveResult);
                Assert.Equal(dense[edit.GlobalIndex], SectionEqualVolumeFixture.GetGlobal(sections, layout, edit.GlobalIndex));
            }

            WorldStateId[] projection = new WorldStateId[dense.Length];
            SectionEqualVolumeFixture.CopyToCanonical(sections, layout, projection, CreateSide16Scratch());
            Assert.Equal(dense, projection);
        }
    }

    [Theory]
    [InlineData((int)SectionEqualVolumeLayout.OneSide32)]
    [InlineData((int)SectionEqualVolumeLayout.EightSide16)]
    public void WarmedEqualVolumeGetAndCopyHotPathsAllocateNothing(int layoutValue)
    {
        SectionEqualVolumeLayout layout = (SectionEqualVolumeLayout)layoutValue;
        WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed);
        MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
        WorldStateId[] projection = new WorldStateId[canonical.Length];
        WorldStateId[][] scratch = CreateSide16Scratch();

        for (int warmup = 0; warmup < 4; warmup++)
        {
            _ = SectionEqualVolumeFixture.GetGlobal(sections, layout, warmup * 127);
            SectionEqualVolumeFixture.CopyToCanonical(sections, layout, projection, scratch);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        ulong checksum = 0;
        for (int repetition = 0; repetition < 4; repetition++)
        {
            for (int index = 0; index < canonical.Length; index += 127)
            {
                checksum = unchecked((checksum * 31UL) ^ SectionEqualVolumeFixture.GetGlobal(sections, layout, index).Value);
            }

            SectionEqualVolumeFixture.CopyToCanonical(sections, layout, projection, scratch);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.NotEqual(0UL, checksum);
        Assert.Equal(canonical, projection);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void KnownPayloadArithmeticIsExactForOwnedElements(int side)
    {
        SectionGeometry geometry = new(new SectionSide(side));
        int volume = side * side * side;
        MutableSectionBlockStates uniform = new(geometry, default, default);
        Assert.Equal(sizeof(uint), uniform.GetStorageMetrics().KnownPayloadBytes);

        _ = uniform.TrySet(geometry.CreateLocal(1, 0, 0), new WorldStateId(1));
        SectionStorageMetrics paletted = uniform.GetStorageMetrics();
        long packedWordCount = (volume + 63L) / 64L;
        long expectedPaletted = (2L * sizeof(uint)) + (packedWordCount * sizeof(ulong));
        Assert.Equal(expectedPaletted, paletted.KnownPayloadBytes);
        Assert.Equal(2, paletted.OwnedArrayCount);

        MutableSectionBlockStates direct = new(geometry, default, default);
        for (int index = 1; index <= 256; index++)
        {
            _ = direct.TrySet(SectionCandidateFixture.ToLocal(index, side), new WorldStateId(checked((uint)index)));
        }

        SectionStorageMetrics directMetrics = direct.GetStorageMetrics();
        Assert.Equal((long)volume * sizeof(uint), directMetrics.KnownPayloadBytes);
        Assert.Equal(1, directMetrics.OwnedArrayCount);
    }

    private static WorldStateId[][] CreateSide16Scratch()
    {
        return [.. Enumerable.Range(0, 8).Select(_ => new WorldStateId[16 * 16 * 16])];
    }
}
