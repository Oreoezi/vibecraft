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
        MutableSectionBlockStates section = new(geometry, new WorldStateId(7), SectionRevision.Initial);

        Assert.Equal(side * side * side, section.Count);
        Assert.Equal(SectionBlockStorageKind.Uniform, section.StorageKind);
        Assert.Equal(new WorldStateId(7), section.Get(geometry.CreateLocal(0, 0, 0)));
        Assert.Equal(SectionWriteResult.Unchanged, section.TrySet(geometry.CreateLocal(1, 2, 3), new WorldStateId(7)));
        Assert.Equal(SectionRevision.Initial, section.Revision);

        Assert.Equal(SectionWriteResult.Changed, section.TrySet(geometry.CreateLocal(1, 2, 3), new WorldStateId(uint.MaxValue)));
        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        Assert.Equal(new SectionRevision(1), section.Revision);
        Assert.Equal(new WorldStateId(uint.MaxValue), section.Get(geometry.CreateLocal(1, 2, 3)));

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
            WorldStateId state = new(checked((uint)index));
            Assert.Equal(SectionWriteResult.Changed, section.TrySet(SectionCandidateFixture.ToLocal(index, side), state));

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
                    new WorldStateId(checked((uint)expectedIndex)),
                    section.Get(SectionCandidateFixture.ToLocal(expectedIndex, side)));
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
        WorldStateId[] expected = new WorldStateId[section.Count];
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
            WorldStateId state = new(value);
            SectionWriteResult result = section.TrySet(SectionCandidateFixture.ToLocal(index, side), state);
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

        WorldStateId[] actual = new WorldStateId[section.Count];
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

        Assert.Equal(SectionWriteResult.Changed, section.TrySet(edited, new WorldStateId(9)));
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
        MutableSectionBlockStates section = new(geometry, new WorldStateId(50), default);
        LocalBlock first = geometry.CreateLocal(0, 0, 0);
        LocalBlock second = geometry.CreateLocal(1, 0, 0);
        _ = section.TrySet(first, new WorldStateId(100));
        _ = section.TrySet(second, new WorldStateId(5));

        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        WorldStateId[] before = new WorldStateId[snapshot.Count];
        snapshot.CopyTo(before);
        Assert.Equal(SectionBlockStorageKind.Paletted, snapshot.StorageKind);
        Assert.Equal(0, snapshot.GetStorageMetrics().ReverseLookupEntryCount);
        Assert.Equal([5U, 50U, 100U], GetSnapshotPalette(snapshot));

        _ = section.TrySet(first, new WorldStateId(200));
        _ = section.TrySet(second, new WorldStateId(200));
        WorldStateId[] after = new WorldStateId[snapshot.Count];
        snapshot.CopyTo(after);

        Assert.Equal(before, after);
        Assert.Equal(new WorldStateId(100), snapshot.Get(first));
        Assert.Equal(new WorldStateId(5), snapshot.Get(second));
    }

    [Fact]
    public void PalettedSnapshotOwnsSemanticInputAndCanonicalizesPalette()
    {
        WorldStateId[] semantic = SectionCandidateFixture.CreateStates(
            SectionGeometry.Side16,
            SectionFixtureKind.PaletteBoundary,
            paletteSize: 3);
        semantic[0] = new WorldStateId(99);
        semantic[1] = new WorldStateId(1);
        semantic[2] = new WorldStateId(50);
        WorldStateId[] expected = (WorldStateId[])semantic.Clone();

        SectionBlockStateSnapshot snapshot = SectionBlockStateSnapshot.Create(
            SectionGeometry.Side16,
            new SectionRevision(7),
            semantic);
        Array.Fill(semantic, new WorldStateId(uint.MaxValue));

        WorldStateId[] actual = new WorldStateId[expected.Length];
        snapshot.CopyTo(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(SectionBlockStorageKind.Paletted, snapshot.StorageKind);
        Assert.Equal([1U, 2U, 3U, 50U, 99U], GetSnapshotPalette(snapshot));
    }

    [Fact]
    public void DirectSnapshotNeverAliasesCallerArray()
    {
        WorldStateId[] semantic = SectionCandidateFixture.CreateStates(
            SectionGeometry.Side16,
            SectionFixtureKind.HighEntropy);
        WorldStateId[] expected = (WorldStateId[])semantic.Clone();

        SectionBlockStateSnapshot snapshot = SectionBlockStateSnapshot.Create(
            SectionGeometry.Side16,
            new SectionRevision(8),
            semantic);
        Array.Fill(semantic, default);

        WorldStateId[] actual = new WorldStateId[expected.Length];
        snapshot.CopyTo(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(SectionBlockStorageKind.Direct, snapshot.StorageKind);
    }

    [Fact]
    public void LowerLevelCanonicalPalettePathRejectsUnsortedDuplicateOrMissingValues()
    {
        WorldStateId[] states = [.. Enumerable.Repeat(new WorldStateId(1), 4096)];
        states[1] = new WorldStateId(2);

        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(states, [new WorldStateId(2), new WorldStateId(1)]));
        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(states, [new WorldStateId(1), new WorldStateId(1)]));
        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(states, [new WorldStateId(1), new WorldStateId(3)]));
        _ = Assert.Throws<ArgumentException>(() => PalettedBlockStateStorage.FromCanonical(
            states,
            [new WorldStateId(1), new WorldStateId(2), new WorldStateId(3)]));
    }

    [Fact]
    public void SnapshotCompactsStalePaletteToUniform()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, default, default);
        LocalBlock first = geometry.CreateLocal(1, 0, 0);
        LocalBlock second = geometry.CreateLocal(2, 0, 0);
        _ = section.TrySet(first, new WorldStateId(1));
        _ = section.TrySet(second, new WorldStateId(2));
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
            _ = section.TrySet(SectionCandidateFixture.ToLocal(index, 16), new WorldStateId(checked((uint)index)));
        }

        Assert.Equal(SectionBlockStorageKind.Direct, section.StorageKind);
        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        LocalBlock edited = SectionCandidateFixture.ToLocal(1, 16);
        _ = section.TrySet(edited, new WorldStateId(999));

        Assert.Equal(SectionBlockStorageKind.Direct, snapshot.StorageKind);
        Assert.Equal(new WorldStateId(1), snapshot.Get(edited));
    }

    [Fact]
    public void MutableDirectDoesNotDowngradeAndSnapshotStillCompacts()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, default, default);
        for (int index = 1; index <= 256; index++)
        {
            _ = section.TrySet(SectionCandidateFixture.ToLocal(index, 16), new WorldStateId(checked((uint)index)));
        }

        for (int index = 1; index <= 256; index++)
        {
            _ = section.TrySet(SectionCandidateFixture.ToLocal(index, 16), default);
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
                section.TrySet(SectionCandidateFixture.ToLocal(index, 16), new WorldStateId(checked((uint)index))));
        }

        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        Assert.Equal(256, section.GetStorageMetrics().PaletteEntryCount);
        LocalBlock attempted = SectionCandidateFixture.ToLocal(256, 16);
        Assert.Equal(SectionWriteResult.RevisionExhausted, section.TrySet(attempted, new WorldStateId(256)));
        Assert.Equal(SectionBlockStorageKind.Paletted, section.StorageKind);
        Assert.Equal(256, section.GetStorageMetrics().PaletteEntryCount);
        Assert.Equal(default, section.Get(attempted));
        Assert.Equal(new SectionRevision(long.MaxValue), section.Revision);
    }

    [Fact]
    public void ExhaustedRevisionRefusesChangeAtomically()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        MutableSectionBlockStates section = new(geometry, new WorldStateId(1), new SectionRevision(long.MaxValue));
        LocalBlock local = geometry.CreateLocal(4, 5, 6);

        Assert.Equal(SectionWriteResult.Unchanged, section.TrySet(local, new WorldStateId(1)));
        Assert.Equal(SectionWriteResult.RevisionExhausted, section.TrySet(local, new WorldStateId(2)));
        Assert.Equal(new WorldStateId(1), section.Get(local));
        Assert.Equal(SectionBlockStorageKind.Uniform, section.StorageKind);
        Assert.Equal(new SectionRevision(long.MaxValue), section.Revision);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void CopyToValidatesLengthAndUsesXThenZThenYOrder(int side)
    {
        SectionGeometry geometry = Geometry(side);
        WorldStateId[] expected = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.HighEntropy);
        MutableSectionBlockStates section = SectionCandidateFixture.CreateSection(geometry, expected);
        WorldStateId[] tooShort = new WorldStateId[section.Count - 1];
        WorldStateId sentinel = new(uint.MaxValue);
        WorldStateId[] oversized = [.. Enumerable.Repeat(sentinel, section.Count + 3)];

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
        _ = paletted.TrySet(geometry.CreateLocal(1, 2, 3), new WorldStateId(1));
        MutableSectionBlockStates direct = new(geometry, default, default);
        for (int index = 1; index <= 256; index++)
        {
            _ = direct.TrySet(SectionCandidateFixture.ToLocal(index, 16), new WorldStateId(checked((uint)index)));
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
        WorldStateId[] destination = new WorldStateId[uniform.Count];
        LocalBlock local = geometry.CreateLocal(7, 8, 9);

        foreach (IReadOnlySectionBlockStates candidate in candidates)
        {
            _ = candidate.Get(local);
            candidate.CopyTo(destination);
            long before = GC.GetAllocatedBytesForCurrentThread();
            uint checksum = 0;
            for (int iteration = 0; iteration < 64; iteration++)
            {
                checksum ^= candidate.Get(local).Value;
                candidate.CopyTo(destination);
                checksum ^= destination[iteration].Value;
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(checksum);
            Assert.Equal(before, after);
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
            ["VibeCraft.G1.Benchmarks", "VibeCraft.G1.Tests", "VibeCraft.LogicalCodecs"],
            friends);
    }

    private static uint[] GetSnapshotPalette(SectionBlockStateSnapshot snapshot)
    {
        FieldInfo storageField = typeof(SectionBlockStateSnapshot).GetField("_storage", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Snapshot storage field is missing.");
        object storage = storageField.GetValue(snapshot) ?? throw new InvalidOperationException("Snapshot storage is missing.");
        FieldInfo paletteField = storage.GetType().GetField("_palette", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Palette field is missing.");
        WorldStateId[] palette = (WorldStateId[])(paletteField.GetValue(storage) ?? throw new InvalidOperationException("Palette is missing."));
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
