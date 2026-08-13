using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using VibeCraft.Content;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.Sections;

/// <summary>
/// Deterministic conformance guardrails for the proposed E1 corpus. These tests deliberately do
/// not measure or decide performance; the benchmark/report harness owns those observations.
/// </summary>
public sealed class E1CoreDataReportTests
{
    private const string G0FixtureId = "VC-G0-FP-0.1.0";
    private const string SectionFixtureId = "VC-G1-E1-SECTIONS-0.1.0";
    private const string SemanticFingerprintDomain = "VC-G1-E1-SEMANTIC-FP-0.1.0";
    private const ulong Seed = 0x5643424654314531UL;
    private const ulong CubeSeedIncrement = 0x9E3779B97F4A7C15UL;
    private const int CorpusCubeCount = 12_500;
    private const int ReadTraceLength = 65_536;

    public static TheoryData<int> PaletteBoundaries =>
    [
        1, 2, 3, 4, 5, 8, 9, 16, 17, 32, 33, 64, 65, 128, 129, 256, 257,
    ];

    [Fact]
    public void FixedFixtureIdentitiesAndSeedAreExact()
    {
        Assert.Equal("VC-G0-FP-0.1.0", G0FixtureId);
        Assert.Equal(SectionFixtureId, SectionCandidateFixture.FixtureId);
        Assert.Equal(Seed, SectionCandidateFixture.DefaultSeed);
        Assert.Equal("VC-G1-E1-LOGICAL-PROJECTION-0.1.0", CanonicalLogicalProjectionCodecV1.FixtureId);
    }

    [Theory]
    [InlineData(0, (int)SectionFixtureKind.UniformAir)]
    [InlineData(1, (int)SectionFixtureKind.Layered)]
    [InlineData(2, (int)SectionFixtureKind.Mixed)]
    [InlineData(3, (int)SectionFixtureKind.HighEntropy)]
    [InlineData(4, (int)SectionFixtureKind.UniformStone)]
    [InlineData(12_496, (int)SectionFixtureKind.UniformAir)]
    [InlineData(12_497, (int)SectionFixtureKind.Layered)]
    [InlineData(12_498, (int)SectionFixtureKind.Mixed)]
    [InlineData(12_499, (int)SectionFixtureKind.HighEntropy)]
    public void CorpusOrdinalScheduleAndCubeSeedAreMathematicallyFixed(int ordinal, int expectedKind)
    {
        (SectionFixtureKind kind, ulong cubeSeed) = GetCorpusEntry(ordinal);

        Assert.Equal((SectionFixtureKind)expectedKind, kind);
        Assert.Equal(unchecked(Seed + ((ulong)ordinal * CubeSeedIncrement)), cubeSeed);
    }

    [Theory]
    [MemberData(nameof(PaletteBoundaries))]
    public void PaletteBoundaryFixturesRoundTripExactlyForBothCandidateSides(int paletteSize)
    {
        foreach (SectionGeometry geometry in new[] { SectionGeometry.Side16, SectionGeometry.Side32 })
        {
            BlockStateId[] semantic = SectionCandidateFixture.CreateStates(
                geometry,
                SectionFixtureKind.PaletteBoundary,
                paletteSize: paletteSize);
            MutableSectionBlockStates adaptive = SectionCandidateFixture.CreateSection(geometry, semantic);
            SectionBlockStateSnapshot snapshot = adaptive.CaptureSnapshot();
            BlockStateId[] copied = new BlockStateId[semantic.Length];
            snapshot.CopyTo(copied);

            Assert.Equal(paletteSize, copied.Distinct().Count());
            Assert.Equal(semantic, copied);

            CanonicalLogicalProjection first = CreateProjection(
                CreateMap(copied),
                [new LogicalSectionInput(SectionKey(checked((uint)geometry.Side.Value), 0, 0, 0), geometry, copied)]);
            CanonicalLogicalProjection second = CreateProjection(
                CreateMap(semantic),
                [new LogicalSectionInput(SectionKey(checked((uint)geometry.Side.Value), 0, 0, 0), geometry, semantic)]);
            LogicalProjectionEncoding firstEncoding = CanonicalLogicalProjectionCodecV1.Encode(first);
            LogicalProjectionEncoding secondEncoding = CanonicalLogicalProjectionCodecV1.Encode(second);
            LogicalDecodeResult<CanonicalLogicalProjection> decoded = CanonicalLogicalProjectionCodecV1.TryDecode(firstEncoding.Bytes.AsSpan());

            Assert.True(firstEncoding.Bytes.AsSpan().SequenceEqual(secondEncoding.Bytes.AsSpan()));
            Assert.True(decoded.Succeeded);
            Assert.True(firstEncoding.Bytes.AsSpan().SequenceEqual(CanonicalLogicalProjectionCodecV1.Encode(decoded.Value).Bytes.AsSpan()));
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void XThenZThenYIndexingIsExhaustiveBijectionAndAgreesWithFixture(int side)
    {
        SectionGeometry geometry = new(new SectionSide(side));
        bool[] seen = new bool[side * side * side];
        int visited = 0;

        for (int y = 0; y < side; y++)
        {
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    LocalBlock local = geometry.CreateLocal(x, y, z);
                    int expected = x + (side * (z + (side * y)));
                    LocalIndex actual = geometry.GetLocalIndex(local);

                    Assert.Equal(new LocalIndex(expected), actual);
                    Assert.False(seen[actual.Value]);
                    seen[actual.Value] = true;
                    Assert.Equal(local, SectionCandidateFixture.ToLocal(actual, geometry));
                    visited++;
                }
            }
        }

        Assert.Equal(side * side * side, visited);
        Assert.All(seen, Assert.True);
    }

    [Fact]
    public void EightSide16SnapshotsReassembleTheOneSide32SemanticCube()
    {
        BlockStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed, Seed);
        MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, canonical);
        BlockStateId[] reconstructed = CopyLayout(sections, SectionEqualVolumeLayout.EightSide16);

        Assert.Equal(canonical, reconstructed);

        for (int index = 0; index < canonical.Length; index++)
        {
            Assert.Equal(canonical[index], SectionEqualVolumeFixture.GetGlobal(sections, SectionEqualVolumeLayout.EightSide16, index));
        }
    }

    [Fact]
    public void SnapshotCreationComparisonConsumesPerSnapshotMetadataWhileSemanticChecksRemainSeparate()
    {
        BlockStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed, Seed);
        MutableSectionBlockStates[] oneSide32 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.OneSide32, canonical);
        MutableSectionBlockStates[] eightSide16 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, canonical);

        (int Side, long Revision, int Count, SectionBlockStorageKind Kind, long KnownPayloadBytes)[] oneMetadata =
            CaptureSnapshotMetadata(oneSide32);
        (int Side, long Revision, int Count, SectionBlockStorageKind Kind, long KnownPayloadBytes)[] eightMetadata =
            CaptureSnapshotMetadata(eightSide16);

        _ = Assert.Single(oneMetadata);
        Assert.Equal(8, eightMetadata.Length);
        Assert.Equal(32, oneMetadata[0].Side);
        Assert.All(eightMetadata, metadata => Assert.Equal(16, metadata.Side));
        Assert.Equal(SectionEqualVolumeFixture.CubeVolume, oneMetadata.Sum(metadata => metadata.Count));
        Assert.Equal(SectionEqualVolumeFixture.CubeVolume, eightMetadata.Sum(metadata => metadata.Count));
        Assert.NotEqual(oneMetadata, eightMetadata);
        Assert.Equal(canonical, CopyLayout(oneSide32, SectionEqualVolumeLayout.OneSide32));
        Assert.Equal(canonical, CopyLayout(eightSide16, SectionEqualVolumeLayout.EightSide16));
    }

    [Theory]
    [InlineData(0, "f46ed79a49c04dfad1468a281639350f8b5a2c57f0c36cb9170490d46ecaffa9")]
    [InlineData(1, "d7951bab8dfc5ce8f21a7036068ec22c2d68ac7cb08200ee300549819d50da28")]
    [InlineData(2, "a1b3d8d06464ad9d3971f10c0b1b5c8b657c8c065887b903bb77eb2776aa5f1c")]
    [InlineData(3, "68edd29c671913d39253eaca55cb8a57a0de607d2ba0682521764ef4c874b393")]
    public void SemanticFingerprintIsRepresentationIndependentAndPinned(int ordinal, string expectedHash)
    {
        (SectionFixtureKind kind, ulong cubeSeed) = GetCorpusEntry(ordinal);
        BlockStateId[] dense = SectionEqualVolumeFixture.CreateCanonicalCube(kind, cubeSeed);
        MutableSectionBlockStates[] oneSide32 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.OneSide32, dense);
        MutableSectionBlockStates[] eightSide16 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, dense);
        BlockStateId[] copied32 = CopyLayout(oneSide32, SectionEqualVolumeLayout.OneSide32);
        BlockStateId[] copied16 = CopyLayout(eightSide16, SectionEqualVolumeLayout.EightSide16);

        string denseHash = ComputeSemanticFingerprint(ordinal, kind, dense);
        Assert.Equal(dense, copied32);
        Assert.Equal(dense, copied16);
        Assert.Equal(denseHash, ComputeSemanticFingerprint(ordinal, kind, copied32));
        Assert.Equal(denseHash, ComputeSemanticFingerprint(ordinal, kind, copied16));
        Assert.Equal(expectedHash, denseHash);
    }

    [Fact]
    public void LogicalProjectionIsDeterministicForEquivalentHistoryAndRoundTripsPerLayout()
    {
        (_, ulong cubeSeed) = GetCorpusEntry(2);
        BlockStateId[] semantic = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed, cubeSeed);
        WorldStateMap map = CreateMap(semantic);
        MutableSectionBlockStates[] oneSide32 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.OneSide32, semantic);
        MutableSectionBlockStates[] eightSide16 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, semantic);

        LogicalProjectionEncoding first32 = CanonicalLogicalProjectionCodecV1.Encode(CreateProjectionForLayout(map, oneSide32, SectionEqualVolumeLayout.OneSide32, reverseInputs: false));
        LogicalProjectionEncoding second32 = CanonicalLogicalProjectionCodecV1.Encode(CreateProjectionForLayout(map, oneSide32, SectionEqualVolumeLayout.OneSide32, reverseInputs: true));
        LogicalProjectionEncoding first16 = CanonicalLogicalProjectionCodecV1.Encode(CreateProjectionForLayout(map, eightSide16, SectionEqualVolumeLayout.EightSide16, reverseInputs: false));
        LogicalProjectionEncoding second16 = CanonicalLogicalProjectionCodecV1.Encode(CreateProjectionForLayout(map, eightSide16, SectionEqualVolumeLayout.EightSide16, reverseInputs: true));

        Assert.True(first32.Bytes.AsSpan().SequenceEqual(second32.Bytes.AsSpan()));
        Assert.Equal(first32.Digest, second32.Digest);
        Assert.True(first16.Bytes.AsSpan().SequenceEqual(second16.Bytes.AsSpan()));
        Assert.Equal(first16.Digest, second16.Digest);
        Assert.False(first32.Bytes.AsSpan().SequenceEqual(first16.Bytes.AsSpan()));
        Assert.Equal(67_439, first32.Bytes.Length);
        Assert.Equal("21aaa7033ba5516bf2e0014304fbdccbe0e7800d675f0d28bace30648c298c89", first32.Digest.ToString());
        Assert.Equal(69_454, first16.Bytes.Length);
        Assert.Equal("4b795335da58c6dae0117999b4a1c30d0be8c88ea3fccfc67a0dab69f5f7917d", first16.Digest.ToString());
        AssertRoundTripsExactly(first32);
        AssertRoundTripsExactly(first16);
    }

    [Theory]
    [InlineData((int)SectionEqualVolumeLayout.OneSide32, false)]
    [InlineData((int)SectionEqualVolumeLayout.OneSide32, true)]
    [InlineData((int)SectionEqualVolumeLayout.EightSide16, false)]
    [InlineData((int)SectionEqualVolumeLayout.EightSide16, true)]
    public void WarmedSteadyRandomAndLinearAdaptiveReadsAllocateZeroBytes(int layoutValue, bool random)
    {
        SectionEqualVolumeLayout layout = (SectionEqualVolumeLayout)layoutValue;
        BlockStateId[] semantic = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed, Seed);
        MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, semantic);
        int[] trace = CreateReadTrace(random);
        ulong checksum = 0;

        for (int warmup = 0; warmup < 4; warmup++)
        {
            checksum = unchecked((checksum * 1_099_511_628_211UL) + ConsumeReadTrace(sections, layout, trace));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int repetition = 0; repetition < 4; repetition++)
        {
            checksum = unchecked((checksum * 1_099_511_628_211UL) + ConsumeReadTrace(sections, layout, trace));
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.NotEqual(0UL, checksum);
    }

    [Theory]
    [InlineData((int)SectionEditTraceKind.InteriorClusters)]
    [InlineData((int)SectionEditTraceKind.BoundaryClusters)]
    public void FixedReadAndEditTracesAreDeterministicWithProtocolShapes(int traceKindValue)
    {
        BlockStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionFixtureKind.Mixed, Seed);
        int[] linear = CreateReadTrace(random: false);
        int[] random = CreateReadTrace(random: true);
        SectionEditTraceKind traceKind = (SectionEditTraceKind)traceKindValue;
        SectionEdit[] smoke = SectionEqualVolumeFixture.CreateEditTrace(canonical, traceKind, Seed, clusterCount: 2);
        SectionEdit[] full = SectionEqualVolumeFixture.CreateEditTrace(canonical, traceKind, Seed, SectionEqualVolumeFixture.DefaultClusterCount);

        Assert.Equal(ReadTraceLength, linear.Length);
        Assert.Equal(ReadTraceLength, random.Length);
        Assert.Equal(linear, CreateReadTrace(random: false));
        Assert.Equal(random, CreateReadTrace(random: true));
        Assert.All(linear, index => Assert.InRange(index, 0, SectionEqualVolumeFixture.CubeVolume - 1));
        Assert.All(random, index => Assert.InRange(index, 0, SectionEqualVolumeFixture.CubeVolume - 1));
        Assert.Equal(2 * SectionEqualVolumeFixture.EditsPerCluster, smoke.Length);
        Assert.Equal(SectionEqualVolumeFixture.DefaultClusterCount * SectionEqualVolumeFixture.EditsPerCluster, full.Length);
        Assert.Equal(smoke, SectionEqualVolumeFixture.CreateEditTrace(canonical, traceKind, Seed, clusterCount: 2));
        Assert.Equal(full, SectionEqualVolumeFixture.CreateEditTrace(canonical, traceKind, Seed, SectionEqualVolumeFixture.DefaultClusterCount));
    }

    private static (SectionFixtureKind Kind, ulong CubeSeed) GetCorpusEntry(int ordinal)
    {
        Assert.InRange(ordinal, 0, CorpusCubeCount - 1);
        SectionFixtureKind kind = (ordinal % 4) switch
        {
            0 => ((ordinal / 4) & 1) == 0 ? SectionFixtureKind.UniformAir : SectionFixtureKind.UniformStone,
            1 => SectionFixtureKind.Layered,
            2 => SectionFixtureKind.Mixed,
            _ => SectionFixtureKind.HighEntropy,
        };
        return (kind, unchecked(Seed + ((ulong)ordinal * CubeSeedIncrement)));
    }

    private static (int Side, long Revision, int Count, SectionBlockStorageKind Kind, long KnownPayloadBytes)[] CaptureSnapshotMetadata(
        MutableSectionBlockStates[] sections)
    {
        return
        [
            .. sections.Select(section =>
            {
                SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
                return (
                    snapshot.Geometry.Side.Value,
                    snapshot.Revision.Value,
                    snapshot.Count,
                    snapshot.StorageKind,
                    snapshot.GetStorageMetrics().KnownPayloadBytes);
            }),
        ];
    }

    private static BlockStateId[] CopyLayout(MutableSectionBlockStates[] sections, SectionEqualVolumeLayout layout)
    {
        BlockStateId[] copied = new BlockStateId[SectionEqualVolumeFixture.CubeVolume];
        SectionEqualVolumeFixture.CopyToCanonical(sections, layout, copied, CreateSide16Scratch());
        return copied;
    }

    private static CanonicalLogicalProjection CreateProjectionForLayout(
        WorldStateMap map,
        MutableSectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        bool reverseInputs)
    {
        LogicalSectionInput[] inputs = new LogicalSectionInput[sections.Length];
        for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            SectionEqualVolumeFixture.GetSectionCoordinates(layout, sectionIndex, out int x, out int y, out int z);
            SectionBlockStateSnapshot snapshot = sections[sectionIndex].CaptureSnapshot();
            BlockStateId[] semantic = new BlockStateId[snapshot.Count];
            snapshot.CopyTo(semantic);
            inputs[sectionIndex] = new LogicalSectionInput(
                SectionKey(0, x, y, z),
                snapshot.Geometry,
                semantic);
        }

        return CreateProjection(map, reverseInputs ? inputs.Reverse() : inputs);
    }

    private static CanonicalLogicalProjection CreateProjection(WorldStateMap map, IEnumerable<LogicalSectionInput> inputs)
    {
        return CanonicalLogicalProjection.Create(map, inputs);
    }

    private static LogicalRecordKey SectionKey(uint dimension, long x, long y, long z)
    {
        return new LogicalRecordKey(LogicalRecordKind.SectionState, new DimensionId(dimension), new SectionCoord(x, y, z));
    }

    private static WorldStateMap CreateMap(ReadOnlySpan<BlockStateId> states)
    {
        HashSet<uint> ids = [0U];
        foreach (BlockStateId state in states)
        {
            _ = ids.Add(state.Value);
        }

        return WorldStateMap.Restore(ids.OrderBy(id => id).Select(id => new WorldStateBinding(
            new BlockStateId(id),
            id == 0 ? CanonicalBlockState.Air : new CanonicalBlockState(NamespacedContentId.Create("fixture", $"state-{id}"), []))));
    }

    private static string ComputeSemanticFingerprint(int ordinal, SectionFixtureKind kind, ReadOnlySpan<BlockStateId> states)
    {
        Assert.Equal(SectionEqualVolumeFixture.CubeVolume, states.Length);
        byte[] fixedFields = new byte[sizeof(ulong) + sizeof(uint) + sizeof(byte)];
        BinaryPrimitives.WriteUInt64LittleEndian(fixedFields, Seed);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedFields.AsSpan(sizeof(ulong)), checked((uint)ordinal));
        fixedFields[^1] = (byte)kind;

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(SemanticFingerprintDomain));
        hash.AppendData([0]);
        hash.AppendData(Encoding.UTF8.GetBytes(SectionFixtureId));
        hash.AppendData([0]);
        hash.AppendData(fixedFields);
        Span<byte> encodedState = stackalloc byte[sizeof(uint)];
        foreach (BlockStateId state in states)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(encodedState, state.Value);
            hash.AppendData(encodedState);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AssertRoundTripsExactly(LogicalProjectionEncoding encoding)
    {
        LogicalDecodeResult<CanonicalLogicalProjection> decoded = CanonicalLogicalProjectionCodecV1.TryDecode(encoding.Bytes.AsSpan());
        Assert.True(decoded.Succeeded);
        Assert.True(encoding.Bytes.AsSpan().SequenceEqual(CanonicalLogicalProjectionCodecV1.Encode(decoded.Value).Bytes.AsSpan()));
    }

    private static int[] CreateReadTrace(bool random)
    {
        int[] trace = new int[ReadTraceLength];
        if (!random)
        {
            for (int index = 0; index < trace.Length; index++)
            {
                trace[index] = index % SectionEqualVolumeFixture.CubeVolume;
            }

            return trace;
        }

        ulong state = Seed;
        for (int index = 0; index < trace.Length; index++)
        {
            state += CubeSeedIncrement;
            ulong mixed = state;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            trace[index] = (int)(mixed % SectionEqualVolumeFixture.CubeVolume);
        }

        return trace;
    }

    private static ulong ConsumeReadTrace(MutableSectionBlockStates[] sections, SectionEqualVolumeLayout layout, int[] trace)
    {
        ulong checksum = 0;
        for (int index = 0; index < trace.Length; index++)
        {
            checksum = unchecked((checksum * 31UL) ^ SectionEqualVolumeFixture.GetGlobalUnchecked(sections, layout, trace[index]).Value);
        }

        return checksum;
    }

    private static BlockStateId[][] CreateSide16Scratch()
    {
        return [.. Enumerable.Range(0, 8).Select(_ => new BlockStateId[16 * 16 * 16])];
    }
}
