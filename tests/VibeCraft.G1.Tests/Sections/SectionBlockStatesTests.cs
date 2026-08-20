using System.Reflection;
using System.Runtime.CompilerServices;
using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;
using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.Sections;

public sealed class SectionBlockStatesTests
{
    [Fact]
    public void DefaultGeometryIsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new MutableSectionBlockStates(default, default, default));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void UniformFirstDifferenceBecomesPaletted(int side)
    {
        SectionGeometry geometry = Geometry(side);
        MutableSectionBlockStates section = new(geometry, new BlockStateId(7), SectionRevision.Initial);

        Assert.Equal(side * side * side, section.Count);
        Assert.Equal(SectionBlockStorageKind.Uniform, section.StorageKind);
        Assert.Equal(new BlockStateId(7), section.Get(geometry.CreateLocal(0, 0, 0)));
        Assert.Equal(SectionWriteResult.Unchanged, section.TrySet(geometry.CreateLocal(1, 2, 3), new BlockStateId(7)));
        Assert.Equal(SectionRevision.Initial, section.Revision);

        Assert.Equal(SectionWriteResult.Changed, section.TrySet(geometry.CreateLocal(1, 2, 3), new BlockStateId(uint.MaxValue)));
        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        Assert.Equal(new SectionRevision(1), section.Revision);
        Assert.Equal(new BlockStateId(uint.MaxValue), section.Get(geometry.CreateLocal(1, 2, 3)));

        SectionStorageMetrics metrics = section.GetStorageMetrics();
        Assert.Equal(2, metrics.PaletteEntryCount);
        Assert.Equal(2, metrics.PaletteCapacity);
        Assert.Equal(1, metrics.BitsPerEntry);
        Assert.Equal(2, metrics.ReverseLookupEntryCount);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void EveryPaletteGrowthThresholdPreservesValuesAndPromotesAt257(int side)
    {
        SectionGeometry geometry = Geometry(side);
        MutableSectionBlockStates section = new(geometry, default, SectionRevision.Initial);

        for (int distinctCount = 2; distinctCount <= 257; distinctCount++)
        {
            int index = distinctCount - 1;
            BlockStateId state = new(checked((uint)index));
            Assert.Equal(SectionWriteResult.Changed, section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), state));

            SectionStorageMetrics metrics = section.GetStorageMetrics();
            if (distinctCount <= 256)
            {
                Assert.Equal(SectionBlockStorageKind.Paletted, metrics.Kind);
                Assert.Equal(distinctCount, metrics.PaletteEntryCount);
                Assert.Equal(ExpectedPaletteCapacity(distinctCount), metrics.PaletteCapacity);
                Assert.Equal(ExpectedBits(distinctCount), metrics.BitsPerEntry);
            }
            else
            {
                Assert.Equal(SectionBlockStorageKind.Direct, metrics.Kind);
            }

            for (int expectedIndex = 0; expectedIndex < distinctCount; expectedIndex++)
            {
                Assert.Equal(
                    new BlockStateId(checked((uint)expectedIndex)),
                    section.Get(SectionCandidateFixture.ToLocal(new LocalIndex(expectedIndex), geometry)));
            }
        }
    }

    [Theory]
    [InlineData(16, 7000)]
    [InlineData(32, 14000)]
    public void SeededRandomizedEditsMatchDenseReference(int side, int operationCount)
    {
        SectionGeometry geometry = Geometry(side);
        MutableSectionBlockStates section = new(geometry, default, SectionRevision.Initial);
        BlockStateId[] expected = new BlockStateId[section.Count];
        ulong random = 0xA0761D6478BD642FUL;
        long changed = 0;

        for (int operation = 0; operation < operationCount; operation++)
        {
            ulong sample = Next(ref random);
            int index = checked((int)(sample % (uint)section.Count));
            uint value = (operation % 17) switch
            {
                0 => 0U,
                1 => uint.MaxValue,
                _ => checked((uint)(Next(ref random) % 400UL)),
            };
            BlockStateId state = new(value);
            SectionWriteResult result = section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), state);
            if (expected[index].Equals(state))
            {
                Assert.Equal(SectionWriteResult.Unchanged, result);
            }
            else
            {
                Assert.Equal(SectionWriteResult.Changed, result);
                expected[index] = state;
                changed++;
            }
        }

        BlockStateId[] actual = new BlockStateId[section.Count];
        section.CopyTo(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(new SectionRevision(changed), section.Revision);
    }

    [Fact]
    public void MutablePaletteDoesNotDowngradeOrCompactOnDeletion()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, default, default);
        LocalBlock edited = geometry.CreateLocal(3, 4, 5);

        Assert.Equal(SectionWriteResult.Changed, section.TrySet(edited, new BlockStateId(9)));
        Assert.Equal(SectionWriteResult.Changed, section.TrySet(edited, default));

        SectionStorageMetrics metrics = section.GetStorageMetrics();
        Assert.Equal(SectionBlockStorageKind.Paletted, metrics.Kind);
        Assert.Equal(2, metrics.PaletteEntryCount);
        Assert.Equal(new SectionRevision(2), section.Revision);
    }

    [Fact]
    public void SnapshotCompactsSortsAndRemainsImmutable()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, new BlockStateId(50), default);
        LocalBlock first = geometry.CreateLocal(0, 0, 0);
        LocalBlock second = geometry.CreateLocal(1, 0, 0);
        _ = section.TrySet(first, new BlockStateId(100));
        _ = section.TrySet(second, new BlockStateId(5));

        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        BlockStateId[] before = new BlockStateId[snapshot.Count];
        snapshot.CopyTo(before);
        Assert.Equal(SectionBlockStorageKind.Paletted, snapshot.StorageKind);
        Assert.Equal(0, snapshot.GetStorageMetrics().ReverseLookupEntryCount);
        Assert.Equal([5U, 50U, 100U], GetSnapshotPalette(snapshot));

        _ = section.TrySet(first, new BlockStateId(200));
        _ = section.TrySet(second, new BlockStateId(200));
        BlockStateId[] after = new BlockStateId[snapshot.Count];
        snapshot.CopyTo(after);

        Assert.Equal(before, after);
        Assert.Equal(new BlockStateId(100), snapshot.Get(first));
        Assert.Equal(new BlockStateId(5), snapshot.Get(second));
    }

    [Fact]
    public void PalettedSnapshotOwnsSemanticInputAndCanonicalizesPalette()
    {
        BlockStateId[] semantic = SectionCandidateFixture.CreateStates(
            SectionGeometry.Side16,
            SectionFixtureKind.PaletteBoundary,
            paletteSize: 3);
        semantic[0] = new BlockStateId(99);
        semantic[1] = new BlockStateId(1);
        semantic[2] = new BlockStateId(50);
        BlockStateId[] expected = (BlockStateId[])semantic.Clone();

        SectionBlockStateSnapshot snapshot = SectionBlockStateSnapshot.Create(
            SectionGeometry.Side16,
            new SectionRevision(7),
            semantic);
        Array.Fill(semantic, new BlockStateId(uint.MaxValue));

        BlockStateId[] actual = new BlockStateId[expected.Length];
        snapshot.CopyTo(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(SectionBlockStorageKind.Paletted, snapshot.StorageKind);
        Assert.Equal([1U, 2U, 3U, 50U, 99U], GetSnapshotPalette(snapshot));
    }

    [Fact]
    public void DirectSnapshotNeverAliasesCallerArray()
    {
        BlockStateId[] semantic = SectionCandidateFixture.CreateStates(
            SectionGeometry.Side16,
            SectionFixtureKind.HighEntropy);
        BlockStateId[] expected = (BlockStateId[])semantic.Clone();

        SectionBlockStateSnapshot snapshot = SectionBlockStateSnapshot.Create(
            SectionGeometry.Side16,
            new SectionRevision(8),
            semantic);
        Array.Fill(semantic, default);

        BlockStateId[] actual = new BlockStateId[expected.Length];
        snapshot.CopyTo(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(SectionBlockStorageKind.Direct, snapshot.StorageKind);
    }

    [Fact]
    public void LowerLevelCanonicalPalettePathRejectsUnsortedDuplicateOrMissingValues()
    {
        BlockStateId[] states = [.. Enumerable.Repeat(new BlockStateId(1), 4096)];
        states[1] = new BlockStateId(2);

        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(states, [new BlockStateId(2), new BlockStateId(1)]));
        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(states, [new BlockStateId(1), new BlockStateId(1)]));
        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(states, [new BlockStateId(1), new BlockStateId(3)]));
        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(
            states,
            [new BlockStateId(1), new BlockStateId(2), new BlockStateId(3)]));
    }

    [Fact]
    public void SnapshotCompactsStalePaletteToUniform()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, default, default);
        LocalBlock first = geometry.CreateLocal(1, 0, 0);
        LocalBlock second = geometry.CreateLocal(2, 0, 0);
        _ = section.TrySet(first, new BlockStateId(1));
        _ = section.TrySet(second, new BlockStateId(2));
        _ = section.TrySet(first, default);
        _ = section.TrySet(second, default);

        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        Assert.Equal(SectionBlockStorageKind.Uniform, snapshot.StorageKind);
        Assert.Equal(sizeof(uint), snapshot.GetStorageMetrics().KnownPayloadBytes);
    }

    [Fact]
    public void DirectSnapshotIsDeepCopied()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, default, default);
        for (int index = 1; index <= 256; index++)
        {
            _ = section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), new BlockStateId(checked((uint)index)));
        }

        Assert.Equal(SectionBlockStorageKind.Direct, section.StorageKind);
        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        LocalBlock edited = SectionCandidateFixture.ToLocal(new LocalIndex(1), geometry);
        _ = section.TrySet(edited, new BlockStateId(999));

        Assert.Equal(SectionBlockStorageKind.Direct, snapshot.StorageKind);
        Assert.Equal(new BlockStateId(1), snapshot.Get(edited));
    }

    [Fact]
    public void MutableDirectDoesNotDowngradeAndSnapshotStillCompacts()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, default, default);
        for (int index = 1; index <= 256; index++)
        {
            _ = section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), new BlockStateId(checked((uint)index)));
        }

        for (int index = 1; index <= 256; index++)
        {
            _ = section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), default);
        }

        Assert.Equal(SectionBlockStorageKind.Direct, section.StorageKind);
        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        Assert.Equal(SectionBlockStorageKind.Uniform, snapshot.StorageKind);
    }

    [Fact]
    public void ExhaustionMakesPaletteToDirectPromotionAtomic()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(
            geometry,
            default,
            new SectionRevision(long.MaxValue - 255));
        for (int index = 1; index <= 255; index++)
        {
            Assert.Equal(
                SectionWriteResult.Changed,
                section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), new BlockStateId(checked((uint)index))));
        }

        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        Assert.Equal(256, section.GetStorageMetrics().PaletteEntryCount);
        LocalBlock attempted = SectionCandidateFixture.ToLocal(new LocalIndex(256), geometry);
        Assert.Equal(SectionWriteResult.RevisionExhausted, section.TrySet(attempted, new BlockStateId(256)));
        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        Assert.Equal(256, section.GetStorageMetrics().PaletteEntryCount);
        Assert.Equal(default, section.Get(attempted));
        Assert.Equal(new SectionRevision(long.MaxValue), section.Revision);
    }

    [Fact]
    public void ExhaustedRevisionRefusesChangeAtomically()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, new BlockStateId(1), new SectionRevision(long.MaxValue));
        LocalBlock local = geometry.CreateLocal(4, 5, 6);

        Assert.Equal(SectionWriteResult.Unchanged, section.TrySet(local, new BlockStateId(1)));
        Assert.Equal(SectionWriteResult.RevisionExhausted, section.TrySet(local, new BlockStateId(2)));
        Assert.Equal(new BlockStateId(1), section.Get(local));
        Assert.Equal(SectionBlockStorageKind.Uniform, section.StorageKind);
        Assert.Equal(new SectionRevision(long.MaxValue), section.Revision);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void CopyToValidatesLengthAndUsesXThenZThenYOrder(int side)
    {
        SectionGeometry geometry = Geometry(side);
        BlockStateId[] expected = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.HighEntropy);
        MutableSectionBlockStates section = SectionCandidateFixture.CreateSection(geometry, expected);
        BlockStateId[] tooShort = new BlockStateId[section.Count - 1];
        BlockStateId sentinel = new(uint.MaxValue);
        BlockStateId[] oversized = [.. Enumerable.Repeat(sentinel, section.Count + 3)];

        _ = Assert.Throws<ArgumentException>(() => section.CopyTo(tooShort));
        section.CopyTo(oversized);

        Assert.Equal(expected, oversized[..section.Count]);
        Assert.All(oversized[section.Count..], value => Assert.Equal(sentinel, value));
        for (int y = 0; y < side; y++)
        {
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    int expectedIndex = x + (side * (z + (side * y)));
                    Assert.Equal(expected[expectedIndex], section.Get(geometry.CreateLocal(x, y, z)));
                }
            }
        }
    }

    [Fact]
    public void WarmedReadsAndCopiesAllocateNothing()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates uniform = new(geometry, default, default);
        MutableSectionBlockStates paletted = new(geometry, default, default);
        _ = paletted.TrySet(geometry.CreateLocal(1, 2, 3), new BlockStateId(1));
        MutableSectionBlockStates direct = new(geometry, default, default);
        for (int index = 1; index <= 256; index++)
        {
            _ = direct.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), new BlockStateId(checked((uint)index)));
        }

        IReadOnlySectionBlockStates[] candidates =
        [
            uniform,
            paletted,
            direct,
            uniform.CaptureSnapshot(),
            paletted.CaptureSnapshot(),
            direct.CaptureSnapshot(),
        ];
        BlockStateId[] destination = new BlockStateId[uniform.Count];
        LocalBlock local = geometry.CreateLocal(7, 8, 9);

        uint warmupChecksum = 0;
        foreach (IReadOnlySectionBlockStates candidate in candidates)
        {
            for (int warmup = 0; warmup < 64; warmup++)
            {
                warmupChecksum ^= candidate.Get(local).Value;
                candidate.CopyTo(destination);
                warmupChecksum ^= destination[warmup].Value;
            }
        }

        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        GC.KeepAlive(warmupChecksum);

        foreach (IReadOnlySectionBlockStates candidate in candidates)
        {
            uint checksum = 0;
            long allocatedBytes = long.MaxValue;
            // Tier promotion may allocate once on the test thread. A real hot-path allocation
            // recurs and therefore cannot satisfy any of these bounded identical probes.
            for (int probe = 0; probe < 8 && allocatedBytes != 0; probe++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int iteration = 0; iteration < 64; iteration++)
                {
                    checksum ^= candidate.Get(local).Value;
                    candidate.CopyTo(destination);
                    checksum ^= destination[iteration].Value;
                }

                allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            GC.KeepAlive(checksum);
            Assert.Equal(0, allocatedBytes);
        }
    }

    [Fact]
    public void CandidateTypesAreInternalAndDenseArraysContainOnlyValueTypes()
    {
        Type[] candidateTypes =
        [
            typeof(MutableSectionBlockStates),
            typeof(SectionBlockStateSnapshot),
            typeof(UniformBlockStateStorage),
            typeof(PalettedBlockStateStorage),
            typeof(DirectBlockStateStorage),
            typeof(PackedPaletteIndices),
        ];

        Assert.All(candidateTypes, type => Assert.False(type.IsPublic));
        foreach (Type type in candidateTypes)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.DoesNotContain(fields, field => field.Name.Contains("entity", StringComparison.OrdinalIgnoreCase) || field.Name.Contains("sparse", StringComparison.OrdinalIgnoreCase));
            foreach (FieldInfo field in fields.Where(field => field.FieldType.IsArray))
            {
                Type elementType = field.FieldType.GetElementType() ?? throw new InvalidOperationException();
                Assert.True(elementType.IsValueType, $"{type.Name}.{field.Name} stores per-voxel references.");
            }
        }
    }

    [Fact]
    public void WorldModelExposesInternalsOnlyToTheG1ExperimentConsumers()
    {
        string[] friends =
        [
            .. typeof(MutableSectionBlockStates).Assembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            ["VibeCraft.G1.Benchmarks", "VibeCraft.G1.Tests"],
            friends);
    }

    private static uint[] GetSnapshotPalette(SectionBlockStateSnapshot snapshot)
    {
        FieldInfo storageField = typeof(SectionBlockStateSnapshot).GetField("_storage", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Snapshot storage field is missing.");
        object storage = storageField.GetValue(snapshot) ?? throw new InvalidOperationException("Snapshot storage is missing.");
        FieldInfo paletteField = storage.GetType().GetField("_palette", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Palette field is missing.");
        BlockStateId[] palette = (BlockStateId[])(paletteField.GetValue(storage) ?? throw new InvalidOperationException("Palette is missing."));
        int count = snapshot.GetStorageMetrics().PaletteEntryCount;
        return [.. palette[..count].Select(value => value.Value)];
    }

    private static SectionGeometry Geometry(int side)
    {
        return new SectionGeometry(new SectionSide(side));
    }

    private static byte ExpectedBits(int paletteCount)
    {
        int bits = 1;
        while ((1 << bits) < paletteCount)
        {
            bits++;
        }

        return checked((byte)bits);
    }

    private static int ExpectedPaletteCapacity(int paletteCount)
    {
        int capacity = 2;
        while (capacity < paletteCount)
        {
            capacity *= 2;
        }

        return capacity;
    }

    private static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
