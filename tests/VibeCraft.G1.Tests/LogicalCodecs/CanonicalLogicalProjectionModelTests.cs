using VibeCraft.Content;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Time;
using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.LogicalCodecs;

public sealed class CanonicalLogicalProjectionModelTests
{
    [Fact]
    public void UnorderedEquivalentInputsCanonicalizeToTheSameProjection()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        LogicalSectionInput first = CreateInput(
            new LogicalRecordKey(LogicalRecordKind.SectionState, new DimensionId(7), new SectionCoord(5, -1, 3)),
            geometry,
            CreateStates(geometry, 7, 2, 0));
        LogicalSectionInput second = CreateInput(
            new LogicalRecordKey(LogicalRecordKind.SectionState, new DimensionId(2), new SectionCoord(-4, 8, -6)),
            geometry,
            CreateStates(geometry, 2, 7, 0));

        CanonicalLogicalProjection forward = CanonicalLogicalProjection.Create(CreateMap(0, 2, 7), [first, second]);
        CanonicalLogicalProjection reverse = CanonicalLogicalProjection.Create(CreateMap(0, 2, 7), [second, first]);

        AssertProjectionEqual(forward, reverse);
        Assert.Equal(second.Key, forward.Sections[0].Key);
        Assert.Equal(first.Key, forward.Sections[1].Key);
    }

    [Fact]
    public void PaletteIsAscendingRegardlessOfForwardOrReverseContainerDiscovery()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        BlockStateId[] semantic = CreateStates(geometry, 0);
        semantic[geometry.GetLocalIndex(geometry.CreateLocal(1, 0, 0)).Value] = new BlockStateId(7);
        semantic[geometry.GetLocalIndex(geometry.CreateLocal(2, 0, 0)).Value] = new BlockStateId(2);
        semantic[geometry.GetLocalIndex(geometry.CreateLocal(3, 0, 0)).Value] = new BlockStateId(7);

        MutableSectionBlockStates forward = CreateWithHistory(geometry, semantic, Enumerable.Range(0, semantic.Length));
        MutableSectionBlockStates reverse = CreateWithHistory(geometry, semantic, Enumerable.Range(0, semantic.Length).Reverse());
        BlockStateId[] forwardStates = CopySnapshot(forward);
        BlockStateId[] reverseStates = CopySnapshot(reverse);

        CanonicalLogicalProjection fromForward = CanonicalLogicalProjection.Create(
            CreateMap(0, 2, 7),
            [CreateInput(Key(1), geometry, forwardStates)]);
        CanonicalLogicalProjection fromReverse = CanonicalLogicalProjection.Create(
            CreateMap(0, 2, 7),
            [CreateInput(Key(1), geometry, reverseStates)]);

        AssertProjectionEqual(fromForward, fromReverse);
        Assert.Equal(new uint[] { 0, 2, 7 }, fromForward.Sections[0].Palette.Select(state => state.Value));
        Assert.Equal(new ushort[] { 0, 2, 1, 2 }, fromForward.Sections[0].PaletteIndices.Take(4));
    }

    [Fact]
    public void MappingBindingsRemainAscendingAndPreserveBlockStateIdGaps()
    {
        WorldStateMap mapping = CreateMap(0, 2, 11, 4096);
        CanonicalLogicalProjection projection = CanonicalLogicalProjection.Create(
            mapping,
            [CreateInput(Key(3), SectionGeometry.Side16, CreateStates(SectionGeometry.Side16, 4096, 2, 0, 11))]);

        Assert.Equal(new uint[] { 0, 2, 11, 4096 }, projection.MappingBindings.Select(binding => binding.Id.Value));
        Assert.Equal(new uint[] { 0, 2, 11, 4096 }, projection.Sections[0].Palette.Select(state => state.Value));
    }

    [Fact]
    public void SparseRecordsAndScheduledTicksUseTheirSpecifiedCanonicalOrder()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        LogicalSparseInput[] sparse =
        [
            Sparse(17),
            Sparse(2),
            Sparse(9),
        ];
        LogicalScheduledTick[] scheduled =
        [
            Tick(LogicalScheduledTickQueueKind.Fluid, 1, -3, 7, 7, "water"),
            Tick(LogicalScheduledTickQueueKind.Block, 11, 2, 5, 5, "later"),
            Tick(LogicalScheduledTickQueueKind.Block, 9, 2, 4, 4, "normal"),
            Tick(LogicalScheduledTickQueueKind.Block, 9, -1, 3, 3, "high"),
            Tick(LogicalScheduledTickQueueKind.Block, 9, -1, 2, 2, "higher-sequence"),
        ];

        LogicalSectionRecord section = CanonicalLogicalProjection.Create(
            CreateMap(0),
            [CreateInput(Key(4), geometry, CreateStates(geometry, 0), sparse, scheduled)]).Sections[0];

        Assert.Collection(
            section.SparseRecords,
            record => Assert.Equal(new LocalIndex(2), record.LocalIndex),
            record => Assert.Equal(new LocalIndex(9), record.LocalIndex),
            record => Assert.Equal(new LocalIndex(17), record.LocalIndex));
        Assert.Equal(new ulong[] { 2, 3, 4, 5, 7 }, section.ScheduledTicks.Select(tick => tick.Sequence));
        LogicalScheduledTickQueueKind[] expectedQueues =
        [
            LogicalScheduledTickQueueKind.Block,
            LogicalScheduledTickQueueKind.Block,
            LogicalScheduledTickQueueKind.Block,
            LogicalScheduledTickQueueKind.Block,
            LogicalScheduledTickQueueKind.Fluid,
        ];
        Assert.Equal(expectedQueues, section.ScheduledTicks.Select(tick => tick.Queue));
    }

    [Fact]
    public void CreationRejectsDuplicateAndAmbiguousRecords()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        LogicalSectionInput input = CreateInput(Key(5), geometry, CreateStates(geometry, 0));

        _ = Assert.Throws<ArgumentException>(() => CanonicalLogicalProjection.Create(CreateMap(0), [input, input]));

        LogicalSectionInput duplicateSparse = CreateInput(
            Key(6),
            geometry,
            CreateStates(geometry, 0),
            [Sparse(2, new byte[] { 1 }), Sparse(2, new byte[] { 2 })]);
        _ = Assert.Throws<ArgumentException>(() => CanonicalLogicalProjection.Create(CreateMap(0), [duplicateSparse]));

        LogicalSectionInput duplicateSequence = CreateInput(
            Key(7),
            geometry,
            CreateStates(geometry, 0),
            [],
            [
                Tick(LogicalScheduledTickQueueKind.Block, 1, 0, 1, 1, "first"),
                Tick(LogicalScheduledTickQueueKind.Fluid, 2, 0, 1, 2, "second"),
            ]);
        _ = Assert.Throws<ArgumentException>(() => CanonicalLogicalProjection.Create(CreateMap(0), [duplicateSequence]));

        LogicalSectionInput duplicateIdentity = CreateInput(
            Key(8),
            geometry,
            CreateStates(geometry, 0),
            [],
            [
                Tick(LogicalScheduledTickQueueKind.Block, 1, 0, 1, 2, "same"),
                Tick(LogicalScheduledTickQueueKind.Block, 2, 1, 2, 2, "same"),
            ]);
        _ = Assert.Throws<ArgumentException>(() => CanonicalLogicalProjection.Create(CreateMap(0), [duplicateIdentity]));
    }

    [Fact]
    public void CreationRejectsBoundedMalformedOverflowedAndUnmappedInput()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        LogicalSectionInput valid = CreateInput(Key(9), geometry, CreateStates(geometry, 0));

        _ = Assert.Throws<ArgumentException>(() => new LogicalSectionInput(
            new LogicalRecordKey(LogicalRecordKind.Undefined, new DimensionId(1), default),
            geometry,
            CreateStates(geometry, 0)));
        _ = Assert.Throws<ArgumentException>(() => new LogicalSectionInput(Key(9), geometry, CreateStates(geometry, 0).Take(geometry.Side.Value)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Sparse(0, new byte[LogicalSparseRecord.MaxPayloadBytes + 1]));
        _ = Assert.Throws<ArgumentException>(() => new LogicalSparseInput(default, default, Array.Empty<byte>()));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalScheduledTick(
            LogicalScheduledTickQueueKind.Block,
            WorldTick.Initial,
            LogicalScheduledTick.MaximumPriority + 1,
            default,
            default,
            NamespacedContentId.Parse("test:type")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalSectionInput(
            Key(9),
            geometry,
            CreateStates(geometry, 0),
            Enumerable.Repeat(Sparse(0), (geometry.Side.Value * geometry.Side.Value * geometry.Side.Value) + 1),
            []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalSectionInput(
            Key(9),
            geometry,
            CreateStates(geometry, 0),
            [],
            Enumerable.Repeat(Tick(LogicalScheduledTickQueueKind.Block, 0, 0, 0, 0, "type"), LogicalScheduledTick.MaxTicksPerSection + 1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalLogicalProjection.Create(
            CreateMap(0),
            Enumerable.Repeat(valid, CanonicalLogicalProjection.MaxSectionRecords + 1)));
        _ = Assert.Throws<OverflowException>(() => new LogicalSectionInput(
            new LogicalRecordKey(
                LogicalRecordKind.SectionState,
                new DimensionId(1),
                new SectionCoord(long.MaxValue, 0, 0)),
            geometry,
            CreateStates(geometry, 0)));
        _ = Assert.Throws<ArgumentException>(() => CanonicalLogicalProjection.Create(
            CreateMap(0),
            [CreateInput(Key(10), geometry, CreateStates(geometry, 99))]));
    }

    [Fact]
    public void SectionInputRejectsInvalidMembersAndGeometryBoundariesImmediately()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        int volume = geometry.Side.Value * geometry.Side.Value * geometry.Side.Value;

        _ = Assert.Throws<ArgumentException>(() => new LogicalSectionInput(
            Key(13),
            geometry,
            CreateStates(geometry, 0),
            [default],
            []));
        _ = Assert.Throws<ArgumentException>(() => new LogicalSectionInput(
            Key(13),
            geometry,
            CreateStates(geometry, 0),
            [],
            [default]));
        _ = Assert.Throws<ArgumentException>(() => new LogicalScheduledTick(
            (LogicalScheduledTickQueueKind)byte.MaxValue,
            WorldTick.Initial,
            0,
            0,
            default,
            NamespacedContentId.Parse("test:type")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalSectionInput(
            Key(13),
            geometry,
            CreateStates(geometry, 0),
            [Sparse(volume)],
            []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalSectionInput(
            Key(13),
            geometry,
            CreateStates(geometry, 0),
            [],
            [Tick(LogicalScheduledTickQueueKind.Block, 0, 0, 0, volume, "type")]));
        _ = Assert.Throws<OverflowException>(() => new LogicalSectionInput(
            new LogicalRecordKey(
                LogicalRecordKind.SectionState,
                new DimensionId(1),
                new SectionCoord(long.MinValue, 0, 0)),
            geometry,
            CreateStates(geometry, 0)));
    }

    [Fact]
    public void InputCollectionsAndPayloadsAreDeepCopiedWithoutAliasing()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        BlockStateId[] states = CreateStates(geometry, 0, 2);
        byte[] payload = [1, 2, 3];
        List<LogicalSparseInput> sparse = [Sparse(4, payload, "chest")];
        List<LogicalScheduledTick> scheduled = [Tick(LogicalScheduledTickQueueKind.Block, 5, 0, 1, 5, "crop")];
        LogicalSectionInput input = CreateInput(Key(11), geometry, states, sparse, scheduled);

        states[0] = new BlockStateId(2);
        payload[0] = 99;
        sparse.Clear();
        scheduled.Clear();

        CanonicalLogicalProjection projection = CanonicalLogicalProjection.Create(CreateMap(0, 2), [input]);
        LogicalSectionRecord section = projection.Sections[0];

        Assert.Equal(new BlockStateId(0), section.States[0]);
        Assert.Equal(NamespacedContentId.Create("test", "chest"), section.SparseRecords[0].Type);
        Assert.Equal(new byte[] { 1, 2, 3 }, section.SparseRecords[0].Payload);
        _ = Assert.Single(section.SparseRecords);
        _ = Assert.Single(section.ScheduledTicks);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void ExactXContiguousZYIndexingIsPreservedForBothSectionSides(int side)
    {
        SectionGeometry geometry = new(new SectionSide(side));
        BlockStateId[] states = CreateStates(geometry, 0);
        (LocalBlock Local, uint State)[] writes =
        [
            (geometry.CreateLocal(0, 0, 0), 1),
            (geometry.CreateLocal(side - 1, 0, 0), 2),
            (geometry.CreateLocal(0, 0, 1), 3),
            (geometry.CreateLocal(0, 1, 0), 4),
            (geometry.CreateLocal(side - 1, side - 1, side - 1), 5),
        ];
        foreach ((LocalBlock local, uint state) in writes)
        {
            states[geometry.GetLocalIndex(local).Value] = new BlockStateId(state);
        }

        LogicalSectionRecord section = CanonicalLogicalProjection.Create(
            CreateMap(0, 1, 2, 3, 4, 5),
            [CreateInput(Key((uint)side), geometry, states)]).Sections[0];

        foreach ((LocalBlock local, uint state) in writes)
        {
            int expectedIndex = local.X + (side * (local.Z + (side * local.Y)));
            Assert.Equal(new LocalIndex(expectedIndex), geometry.GetLocalIndex(local));
            Assert.Equal(new BlockStateId(state), section.GetState(local));
            Assert.Equal(new BlockStateId(state), section.Palette[section.PaletteIndices[expectedIndex]]);
        }
    }

    [Fact]
    public void SnapshotsWithDifferentContainerHistoriesProjectToIdenticalSemanticRecords()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        BlockStateId[] semantic = CreateStates(geometry, 0);
        semantic[1] = new BlockStateId(7);
        semantic[50] = new BlockStateId(2);
        semantic[^1] = new BlockStateId(7);

        MutableSectionBlockStates forward = CreateWithHistory(geometry, semantic, Enumerable.Range(0, semantic.Length));
        MutableSectionBlockStates reverse = CreateWithHistory(geometry, semantic, Enumerable.Range(0, semantic.Length).Reverse());
        BlockStateId[] copiedForward = CopySnapshot(forward);
        BlockStateId[] copiedReverse = CopySnapshot(reverse);

        CanonicalLogicalProjection forwardProjection = CanonicalLogicalProjection.Create(
            CreateMap(0, 2, 7),
            [CreateInput(Key(12), geometry, copiedForward)]);
        CanonicalLogicalProjection reverseProjection = CanonicalLogicalProjection.Create(
            CreateMap(0, 2, 7),
            [CreateInput(Key(12), geometry, copiedReverse)]);

        Assert.Equal(semantic, copiedForward);
        Assert.Equal(semantic, copiedReverse);
        AssertProjectionEqual(forwardProjection, reverseProjection);
    }

    private static LogicalSectionInput CreateInput(
        LogicalRecordKey key,
        SectionGeometry geometry,
        IEnumerable<BlockStateId> states,
        IEnumerable<LogicalSparseInput>? sparse = null,
        IEnumerable<LogicalScheduledTick>? scheduled = null)
    {
        return new LogicalSectionInput(key, geometry, states, sparse ?? [], scheduled ?? []);
    }

    private static LogicalRecordKey Key(uint dimension)
    {
        return new LogicalRecordKey(LogicalRecordKind.SectionState, new DimensionId(dimension), new SectionCoord(0, 0, 0));
    }

    private static LogicalScheduledTick Tick(
        LogicalScheduledTickQueueKind queue,
        ulong dueTick,
        int priority,
        ulong sequence,
        int localIndex,
        string expectedType)
    {
        return new LogicalScheduledTick(
            queue,
            new WorldTick(dueTick),
            priority,
            sequence,
            new LocalIndex(localIndex),
            NamespacedContentId.Create("test", expectedType));
    }

    private static LogicalSparseInput Sparse(int localIndex, ReadOnlyMemory<byte> payload = default, string type = "fixture")
    {
        return new LogicalSparseInput(new LocalIndex(localIndex), NamespacedContentId.Create("test", type), payload);
    }

    private static WorldStateMap CreateMap(params uint[] ids)
    {
        return WorldStateMap.Restore(
        [
            .. ids
                .Append(0U)
                .Distinct()
                .OrderBy(id => id)
                .Select(id => new WorldStateBinding(new BlockStateId(id), CreateState(id))),
        ]);
    }

    private static CanonicalBlockState CreateState(uint id)
    {
        return id == 0
            ? CanonicalBlockState.Air
            : new CanonicalBlockState(NamespacedContentId.Create("test", $"state-{id}"), []);
    }

    private static BlockStateId[] CreateStates(SectionGeometry geometry, params uint[] pattern)
    {
        ArgumentOutOfRangeException.ThrowIfZero(pattern.Length);
        int side = geometry.Side.Value;
        return
        [
            .. Enumerable.Range(0, checked(side * side * side))
                .Select(index => new BlockStateId(pattern[index % pattern.Length])),
        ];
    }

    private static MutableSectionBlockStates CreateWithHistory(
        SectionGeometry geometry,
        BlockStateId[] semantic,
        IEnumerable<int> writeOrder)
    {
        MutableSectionBlockStates section = new(geometry, semantic[0], default);
        foreach (int index in writeOrder)
        {
            if (semantic[index].Equals(semantic[0]))
            {
                continue;
            }

            Assert.Equal(SectionWriteResult.Changed, section.TrySet(SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry), semantic[index]));
        }

        return section;
    }

    private static BlockStateId[] CopySnapshot(MutableSectionBlockStates section)
    {
        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        BlockStateId[] states = new BlockStateId[snapshot.Count];
        snapshot.CopyTo(states);
        return states;
    }

    private static void AssertProjectionEqual(CanonicalLogicalProjection expected, CanonicalLogicalProjection actual)
    {
        Assert.Equal(expected.MappingBindings.Length, actual.MappingBindings.Length);
        for (int bindingIndex = 0; bindingIndex < expected.MappingBindings.Length; bindingIndex++)
        {
            WorldStateBinding expectedBinding = expected.MappingBindings[bindingIndex];
            WorldStateBinding actualBinding = actual.MappingBindings[bindingIndex];
            Assert.Equal(expectedBinding.Id, actualBinding.Id);
            Assert.True(expectedBinding.State.Equals(actualBinding.State));
        }

        Assert.Equal(expected.Sections.Length, actual.Sections.Length);
        for (int sectionIndex = 0; sectionIndex < expected.Sections.Length; sectionIndex++)
        {
            LogicalSectionRecord expectedSection = expected.Sections[sectionIndex];
            LogicalSectionRecord actualSection = actual.Sections[sectionIndex];
            Assert.Equal(expectedSection.Key, actualSection.Key);
            Assert.Equal(expectedSection.Geometry, actualSection.Geometry);
            Assert.Equal(expectedSection.Origin, actualSection.Origin);
            Assert.Equal(expectedSection.EndInclusive, actualSection.EndInclusive);
            Assert.True(expectedSection.States.SequenceEqual(actualSection.States));
            Assert.True(expectedSection.Palette.SequenceEqual(actualSection.Palette));
            Assert.True(expectedSection.PaletteIndices.SequenceEqual(actualSection.PaletteIndices));
            Assert.True(expectedSection.SparseRecords.Select(record => record.LocalIndex).SequenceEqual(actualSection.SparseRecords.Select(record => record.LocalIndex)));
            Assert.True(expectedSection.SparseRecords.Select(record => record.Type).SequenceEqual(actualSection.SparseRecords.Select(record => record.Type)));
            Assert.True(expectedSection.ScheduledTicks.SequenceEqual(actualSection.ScheduledTicks));

            for (int sparseIndex = 0; sparseIndex < expectedSection.SparseRecords.Length; sparseIndex++)
            {
                Assert.True(expectedSection.SparseRecords[sparseIndex].Payload.SequenceEqual(actualSection.SparseRecords[sparseIndex].Payload));
            }
        }
    }
}
