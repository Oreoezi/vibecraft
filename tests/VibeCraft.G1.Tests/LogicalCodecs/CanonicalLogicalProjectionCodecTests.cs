using System.Buffers.Binary;
using System.Reflection;
using VibeCraft.Content;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Time;
using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.LogicalCodecs;

public sealed class CanonicalLogicalProjectionCodecTests
{
    public static TheoryData<string> GoldenFixtures =>
    [
        "projection-minimal-air",
        "projection-palette-properties-257-states",
        "projection-sparse-schedules-negative",
        "projection-multi-record-side32",
    ];

    [Fact]
    public void FixtureIdentityAndHeaderAreExplicitAndStorageNeutral()
    {
        LogicalProjectionEncoding encoded = CanonicalLogicalProjectionCodecV1.Encode(CreateMinimalAirProjection());

        Assert.Equal("VC-G1-E1-LOGICAL-PROJECTION-0.1.0", CanonicalLogicalProjectionCodecV1.FixtureId);
        Assert.Equal("VCG1LP01", CanonicalLogicalProjectionCodecV1.Magic);
        Assert.Equal((ushort)1, CanonicalLogicalProjectionCodecV1.Version);
        Assert.Equal(18, CanonicalLogicalProjectionCodecV1.HeaderSize);
        Assert.Equal("VCG1LP01"u8.ToArray(), encoded.Bytes.AsSpan()[..8].ToArray());
        Assert.Equal((byte)0, encoded.Bytes[8]);
        Assert.Equal((byte)1, encoded.Bytes[9]);
    }

    [Fact]
    public void EncodingResultAndDigestCannotExposeContradictoryOrMutablePublicState()
    {
        Type[] closedTypes =
        [
            typeof(LogicalProjectionEncoding),
            typeof(LogicalProjectionEncodeResult),
            typeof(LogicalProjectionDigest),
        ];
        Assert.All(closedTypes, type => Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)));

        LogicalProjectionEncoding encoding = CanonicalLogicalProjectionCodecV1.Encode(CreateMinimalAirProjection());
        Assert.Equal(LogicalProjectionDigest.Compute(encoding.Bytes.AsSpan()), encoding.Digest);
        Assert.Equal(32, encoding.Digest.Bytes.Length);
        Assert.All(
            typeof(LogicalProjectionEncoding).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => Assert.False(property.CanWrite));
        Assert.All(
            typeof(LogicalProjectionDigest).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => Assert.False(property.CanWrite));
    }

    [Fact]
    public void EquivalentInputOrdersAndContainerHistoriesProduceIdenticalBytesAndDigests()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        WorldStateId[] semantic = States(geometry, 0, 2, 7, 2);
        MutableSectionBlockStates forward = CreateWithHistory(geometry, semantic, Enumerable.Range(0, semantic.Length));
        MutableSectionBlockStates reverse = CreateWithHistory(geometry, semantic, Enumerable.Range(0, semantic.Length).Reverse());
        LogicalSectionInput first = new(Key(9, 4, -2, 3), geometry, Snapshot(forward));
        LogicalSectionInput second = new(Key(2, -4, 3, -1), geometry, States(geometry, 7, 0, 2));

        CanonicalLogicalProjection forwardProjection = CanonicalLogicalProjection.Create(Map(0, 2, 7), [first, second]);
        CanonicalLogicalProjection reverseProjection = CanonicalLogicalProjection.Create(Map(0, 7, 2), [second, new(Key(9, 4, -2, 3), geometry, Snapshot(reverse))]);
        LogicalProjectionEncoding forwardEncoding = CanonicalLogicalProjectionCodecV1.Encode(forwardProjection);
        LogicalProjectionEncoding reverseEncoding = CanonicalLogicalProjectionCodecV1.Encode(reverseProjection);

        Assert.True(forwardEncoding.Bytes.AsSpan().SequenceEqual(reverseEncoding.Bytes.AsSpan()));
        Assert.Equal(forwardEncoding.Digest, reverseEncoding.Digest);
    }

    [Fact]
    public void SemanticChangeChangesBytesAndDigestAndDecodedValueRoundTripsExactly()
    {
        CanonicalLogicalProjection original = CreateSparseSchedulesNegativeProjection();
        LogicalProjectionEncoding first = CanonicalLogicalProjectionCodecV1.Encode(original);
        LogicalDecodeResult<CanonicalLogicalProjection> decoded = CanonicalLogicalProjectionCodecV1.TryDecode(first.Bytes.AsSpan());

        Assert.True(decoded.Succeeded);
        LogicalProjectionEncoding reencoded = CanonicalLogicalProjectionCodecV1.Encode(decoded.Value);
        Assert.True(first.Bytes.AsSpan().SequenceEqual(reencoded.Bytes.AsSpan()));
        Assert.Equal(first.Digest, reencoded.Digest);
        Assert.Equal(first.Digest.ToString(), LogicalProjectionDigest.Parse(first.Digest.ToString()).ToString());
        Assert.False(LogicalProjectionDigest.TryParse(first.Digest.ToString().ToUpperInvariant(), out _));

        LogicalSectionInput changedSection = new(
            original.Sections[0].Key,
            original.Sections[0].Geometry,
            original.Sections[0].States.Select((state, index) => index == 0 ? new WorldStateId(2) : state),
            original.Sections[0].SparseRecords.Select(record => new LogicalSparseInput(record.LocalIndex, record.Type, record.Payload.AsMemory())),
            original.Sections[0].ScheduledTicks);
        CanonicalLogicalProjection changed = CanonicalLogicalProjection.Create(
            WorldStateMap.Restore(original.MappingBindings),
            [changedSection]);
        LogicalProjectionEncoding changedEncoding = CanonicalLogicalProjectionCodecV1.Encode(changed);

        Assert.False(first.Bytes.AsSpan().SequenceEqual(changedEncoding.Bytes.AsSpan()));
        Assert.NotEqual(first.Digest, changedEncoding.Digest);
    }

    [Fact]
    public void SideSixteenAndThirtyTwoAndMoreThanTwoHundredFiftySixPaletteEntriesRoundTrip()
    {
        CanonicalLogicalProjection paletteProjection = CreatePalettePropertiesProjection();
        CanonicalLogicalProjection side32Projection = CreateMultiRecordSide32Projection();

        Assert.Equal(257, paletteProjection.Sections[0].Palette.Length);
        Assert.Equal(32, side32Projection.Sections[0].Geometry.Side.Value);
        Assert.Equal(32_768, side32Projection.Sections[0].States.Length);
        Assert.Equal(
            Enumerable.Range(LogicalScheduledTick.MinimumPriority, LogicalScheduledTick.MaximumPriority - LogicalScheduledTick.MinimumPriority + 1),
            CreateSparseSchedulesNegativeProjection().Sections[0].ScheduledTicks
                .Where(tick => tick.Queue == LogicalScheduledTickQueueKind.Block)
                .Select(tick => tick.Priority)
                .Distinct()
                .OrderBy(priority => priority));

        AssertRoundTrips(paletteProjection);
        AssertRoundTrips(side32Projection);
    }

    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void CommittedLowercaseHexAndSha256GoldensEncodeDecodeAndReencodeExactly(string fixtureStem)
    {
        string expectedHex = ReadFixture($"{fixtureStem}.hex");
        string expectedDigest = ReadFixture($"{fixtureStem}.sha256");
        CanonicalLogicalProjection projection = fixtureStem switch
        {
            "projection-minimal-air" => CreateMinimalAirProjection(),
            "projection-palette-properties-257-states" => CreatePalettePropertiesProjection(),
            "projection-sparse-schedules-negative" => CreateSparseSchedulesNegativeProjection(),
            "projection-multi-record-side32" => CreateMultiRecordSide32Projection(),
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureStem)),
        };

        Assert.All(expectedHex, character => Assert.True(character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.All(expectedDigest, character => Assert.True(character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        LogicalProjectionEncoding encoded = CanonicalLogicalProjectionCodecV1.Encode(projection);
        Assert.Equal(expectedHex, Convert.ToHexStringLower(encoded.Bytes.AsSpan()));
        Assert.Equal(expectedDigest, encoded.Digest.ToString());

        LogicalDecodeResult<CanonicalLogicalProjection> decoded = CanonicalLogicalProjectionCodecV1.TryDecode(Convert.FromHexString(expectedHex));
        Assert.True(decoded.Succeeded);
        Assert.True(encoded.Bytes.AsSpan().SequenceEqual(CanonicalLogicalProjectionCodecV1.Encode(decoded.Value).Bytes.AsSpan()));
    }

    [Fact]
    public void EveryTruncatedPrefixOfFixedAndVariableRecordsFailsWithNoPublishedProjection()
    {
        CanonicalLogicalProjection[] projections = [CreateMinimalAirProjection(), CreateSparseSchedulesNegativeProjection()];
        foreach (CanonicalLogicalProjection projection in projections)
        {
            byte[] encoded = [.. CanonicalLogicalProjectionCodecV1.Encode(projection).Bytes];
            for (int length = 0; length < encoded.Length; length++)
            {
                LogicalDecodeResult<CanonicalLogicalProjection> result = CanonicalLogicalProjectionCodecV1.TryDecode(encoded.AsSpan(0, length));
                Assert.False(result.Succeeded);
                Assert.NotNull(result.Failure);
                _ = Assert.Throws<InvalidOperationException>(() => result.Value);
            }
        }
    }

    [Fact]
    public void EverySingleByteMutationEitherFailsTypedOrReencodesExactly()
    {
        byte[] canonical = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateSparseSchedulesNegativeProjection()).Bytes];
        for (int offset = 0; offset < canonical.Length; offset++)
        {
            byte[] mutated = [.. canonical];
            mutated[offset] ^= 0x5a;

            Exception? exception = Record.Exception(() => CanonicalLogicalProjectionCodecV1.TryDecode(mutated));
            Assert.Null(exception);
            LogicalDecodeResult<CanonicalLogicalProjection> result = CanonicalLogicalProjectionCodecV1.TryDecode(mutated);
            if (result.Succeeded)
            {
                Assert.True(mutated.AsSpan().SequenceEqual(CanonicalLogicalProjectionCodecV1.Encode(result.Value).Bytes.AsSpan()));
            }
            else
            {
                Assert.NotNull(result.Failure);
                _ = Assert.Throws<InvalidOperationException>(() => result.Value);
            }
        }
    }

    [Fact]
    public void ImpossibleCountsFailBeforeLargeCapacityAllocations()
    {
        byte[] header = CanonicalLogicalProjectionCodecV1.Encode(CreateMinimalAirProjection()).Bytes.AsSpan()[..CanonicalLogicalProjectionCodecV1.HeaderSize].ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10), WorldStateMap.MaxTotalStates);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14), 0);
        _ = CanonicalLogicalProjectionCodecV1.TryDecode(header.AsSpan(0, header.Length - 1));

        long beforeMapping = GC.GetAllocatedBytesForCurrentThread();
        LogicalDecodeResult<CanonicalLogicalProjection> mappingResult = CanonicalLogicalProjectionCodecV1.TryDecode(header);
        long mappingAllocation = GC.GetAllocatedBytesForCurrentThread() - beforeMapping;

        Assert.False(mappingResult.Succeeded);
        Assert.Equal(LogicalCodecFailureCode.IncorrectLength, mappingResult.Failure!.Code);
        Assert.Equal(LogicalCodecField.Mapping, mappingResult.Failure.Field);
        Assert.InRange(mappingAllocation, 0, 128 * 1024);

        byte[] schedules = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateSparseSchedulesNegativeProjection()).Bytes];
        int scheduleCountOffset = FirstScheduleOffset(schedules) - sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(schedules.AsSpan(scheduleCountOffset), LogicalScheduledTick.MaxTicksPerSection);
        _ = CanonicalLogicalProjectionCodecV1.TryDecode(schedules.AsSpan(0, schedules.Length - 1));

        long beforeSchedules = GC.GetAllocatedBytesForCurrentThread();
        LogicalDecodeResult<CanonicalLogicalProjection> scheduleResult = CanonicalLogicalProjectionCodecV1.TryDecode(schedules);
        long scheduleAllocation = GC.GetAllocatedBytesForCurrentThread() - beforeSchedules;

        Assert.False(scheduleResult.Succeeded);
        Assert.Equal(LogicalCodecFailureCode.IncorrectLength, scheduleResult.Failure!.Code);
        Assert.Equal(LogicalCodecField.Schedule, scheduleResult.Failure.Field);
        Assert.InRange(scheduleAllocation, 0, 128 * 1024);
    }

    [Fact]
    public void MalformedHeadersVersionsTrailingBytesLimitsAndCanonicalAlternativesAreRejected()
    {
        byte[] encoded = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateSparseSchedulesNegativeProjection()).Bytes];

        byte[] badMagic = [.. encoded];
        badMagic[0] ^= 0xff;
        AssertFailure(badMagic, LogicalCodecFailureCode.InvalidHeader, LogicalCodecField.Header);

        byte[] badVersion = [.. encoded];
        badVersion[9] = 2;
        AssertFailure(badVersion, LogicalCodecFailureCode.UnsupportedVersion, LogicalCodecField.Version);

        byte[] trailing = [.. encoded, 0];
        AssertFailure(trailing, LogicalCodecFailureCode.TrailingData, LogicalCodecField.Projection);

        byte[] oversized = new byte[CanonicalLogicalProjectionCodecV1.MaxEncodedBytes + 1];
        AssertFailure(oversized, LogicalCodecFailureCode.LimitExceeded, LogicalCodecField.Projection);

        int recordOffset = FirstRecordOffset(encoded);
        byte[] badSide = [.. encoded];
        badSide[recordOffset + LogicalRecordKeyCodecV1.EncodedSize] = 15;
        AssertFailure(badSide, LogicalCodecFailureCode.InvalidValue, LogicalCodecField.Side);

        byte[] reversePalette = [.. encoded];
        int paletteOffset = recordOffset + LogicalRecordKeyCodecV1.EncodedSize + 3;
        byte[] firstPaletteEntry = reversePalette.AsSpan(paletteOffset, 4).ToArray();
        Array.Copy(reversePalette, paletteOffset + 4, reversePalette, paletteOffset, 4);
        Array.Copy(firstPaletteEntry, 0, reversePalette, paletteOffset + 4, 4);
        AssertFailure(reversePalette, LogicalCodecFailureCode.NonCanonicalPalette, LogicalCodecField.Palette);

        byte[] badIndex = [.. encoded];
        int voxelOffset = paletteOffset + 12;
        badIndex[voxelOffset] = 0xff;
        badIndex[voxelOffset + 1] = 0xff;
        AssertFailure(badIndex, LogicalCodecFailureCode.IndexOutOfRange, LogicalCodecField.Voxel);

        byte[] badKeyText = [.. encoded];
        int mappingBlockText = 18 + 4 + 2;
        badKeyText[mappingBlockText] = (byte)'A';
        AssertFailure(badKeyText, LogicalCodecFailureCode.InvalidText, LogicalCodecField.ContentKey);

        byte[] badQueue = [.. encoded];
        int queueOffset = FirstScheduleOffset(encoded);
        badQueue[queueOffset] = 2;
        AssertFailure(badQueue, LogicalCodecFailureCode.InvalidEnum, LogicalCodecField.Queue);

        byte[] badPriority = [.. encoded];
        badPriority[queueOffset + 9] = 4;
        AssertFailure(badPriority, LogicalCodecFailureCode.InvalidValue, LogicalCodecField.Priority);

        byte[] mappings = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateMultiRecordSide32Projection()).Bytes];
        int secondMappingId = NextMappingOffset(mappings, 18);
        Array.Copy(mappings, 18, mappings, secondMappingId, 4);
        AssertFailure(mappings, LogicalCodecFailureCode.DuplicateIdentity, LogicalCodecField.Mapping);
    }

    [Fact]
    public void DuplicateAndUnsortedRecordsSparseEntriesAndSchedulesAreRejectedWithoutPartialValues()
    {
        byte[] encoded = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateMultiRecordSide32Projection()).Bytes];
        int firstRecord = FirstRecordOffset(encoded);
        int secondRecord = NextRecordOffset(encoded, firstRecord);

        byte[] duplicateRecord = [.. encoded];
        Array.Copy(duplicateRecord, firstRecord, duplicateRecord, secondRecord, LogicalRecordKeyCodecV1.EncodedSize);
        AssertFailure(duplicateRecord, LogicalCodecFailureCode.DuplicateIdentity, LogicalCodecField.RecordKey);

        byte[] sparseAndSchedules = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateSparseSchedulesNegativeProjection()).Bytes];
        int sparseOffset = FirstSparseOffset(sparseAndSchedules);
        int firstSparse = sparseOffset + 4;
        int secondSparse = NextSparseOffset(sparseAndSchedules, firstSparse);
        byte[] unsortedSparse = [.. sparseAndSchedules];
        unsortedSparse[secondSparse] = 0;
        unsortedSparse[secondSparse + 1] = 0;
        AssertFailure(unsortedSparse, LogicalCodecFailureCode.NonCanonicalOrder, LogicalCodecField.Sparse);

        int scheduleOffset = FirstScheduleOffset(sparseAndSchedules);
        int secondSchedule = NextScheduleOffset(sparseAndSchedules, scheduleOffset);
        byte[] duplicateSequence = [.. sparseAndSchedules];
        Array.Copy(duplicateSequence, scheduleOffset + 10, duplicateSequence, secondSchedule + 10, 8);
        AssertFailure(duplicateSequence, LogicalCodecFailureCode.DuplicateIdentity, LogicalCodecField.Sequence);

        byte[] unsortedSchedule = [.. sparseAndSchedules];
        BinaryPrimitives.WriteUInt64BigEndian(unsortedSchedule.AsSpan(secondSchedule + 1), 1);
        AssertFailure(unsortedSchedule, LogicalCodecFailureCode.NonCanonicalOrder, LogicalCodecField.Schedule);
    }

    [Fact]
    public void UnusedPaletteOversizedPayloadAndDuplicateScheduleIdentityAreRejected()
    {
        byte[] encoded = [.. CanonicalLogicalProjectionCodecV1.Encode(CreateSparseSchedulesNegativeProjection()).Bytes];
        int recordOffset = FirstRecordOffset(encoded);
        int side = encoded[recordOffset + LogicalRecordKeyCodecV1.EncodedSize];
        int volume = side * side * side;
        int paletteCount = ReadUInt16(encoded, recordOffset + LogicalRecordKeyCodecV1.EncodedSize + 1);
        int voxelOffset = recordOffset + LogicalRecordKeyCodecV1.EncodedSize + 3 + (paletteCount * sizeof(uint));
        byte[] unusedPalette = [.. encoded];
        for (int voxelIndex = 0; voxelIndex < volume; voxelIndex++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(unusedPalette.AsSpan(voxelOffset + (voxelIndex * sizeof(ushort))), 0);
        }

        AssertFailure(unusedPalette, LogicalCodecFailureCode.NonCanonicalPalette, LogicalCodecField.Palette);

        byte[] oversizedPayload = [.. encoded];
        int firstSparse = FirstSparseOffset(oversizedPayload) + sizeof(uint);
        int payloadLengthOffset = SkipContentKey(oversizedPayload, firstSparse + sizeof(ushort));
        BinaryPrimitives.WriteUInt32BigEndian(
            oversizedPayload.AsSpan(payloadLengthOffset),
            LogicalSparseRecord.MaxPayloadBytes + 1);
        AssertFailure(oversizedPayload, LogicalCodecFailureCode.LimitExceeded, LogicalCodecField.Payload);

        SectionGeometry geometry = SectionGeometry.Side16;
        LogicalScheduledTick[] ticks =
        [
            new(LogicalScheduledTickQueueKind.Block, new WorldTick(1), 0, 1, 1, ContentKey.Parse("fixture:same")),
            new(LogicalScheduledTickQueueKind.Block, new WorldTick(2), 0, 2, 2, ContentKey.Parse("fixture:same")),
        ];
        CanonicalLogicalProjection duplicateIdentityProjection = CanonicalLogicalProjection.Create(
            Map(0),
            [new(Key(1, 0, 0, 0), geometry, States(geometry, 0), [], ticks)]);
        byte[] duplicateIdentity = [.. CanonicalLogicalProjectionCodecV1.Encode(duplicateIdentityProjection).Bytes];
        int firstSchedule = FirstScheduleOffset(duplicateIdentity);
        int duplicateIdentitySecondSchedule = NextScheduleOffset(duplicateIdentity, firstSchedule);
        BinaryPrimitives.WriteUInt16BigEndian(duplicateIdentity.AsSpan(duplicateIdentitySecondSchedule + 18), 1);
        AssertFailure(duplicateIdentity, LogicalCodecFailureCode.DuplicateIdentity, LogicalCodecField.Schedule);
    }

    private static void AssertRoundTrips(CanonicalLogicalProjection projection)
    {
        LogicalProjectionEncoding encoded = CanonicalLogicalProjectionCodecV1.Encode(projection);
        LogicalDecodeResult<CanonicalLogicalProjection> decoded = CanonicalLogicalProjectionCodecV1.TryDecode(encoded.Bytes.AsSpan());

        Assert.True(decoded.Succeeded);
        Assert.True(encoded.Bytes.AsSpan().SequenceEqual(CanonicalLogicalProjectionCodecV1.Encode(decoded.Value).Bytes.AsSpan()));
    }

    private static void AssertFailure(byte[] bytes, LogicalCodecFailureCode code, LogicalCodecField field)
    {
        LogicalDecodeResult<CanonicalLogicalProjection> result = CanonicalLogicalProjectionCodecV1.TryDecode(bytes);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Equal(code, result.Failure.Code);
        Assert.Equal(field, result.Failure.Field);
        _ = Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    private static CanonicalLogicalProjection CreateMinimalAirProjection()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        return CanonicalLogicalProjection.Create(Map(0), [new(Key(0, 0, 0, 0), geometry, States(geometry, 0))]);
    }

    private static CanonicalLogicalProjection CreatePalettePropertiesProjection()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        WorldStateMap map = WorldStateMap.Restore(
        [
            new WorldStateBinding(new WorldStateId(0), CanonicalBlockState.Air),
            .. Enumerable.Range(1, 256).Select(index => new WorldStateBinding(
                new WorldStateId(checked((uint)index)),
                new CanonicalBlockState(
                    ContentKey.Create("fixture", $"state-{index}"),
                    index == 1
                        ? [
                            BlockStateProperty.Create(ContentKey.Create("fixture", "age"), "seven"),
                            BlockStateProperty.Create(ContentKey.Create("fixture", "lit"), "true"),
                        ]
                        : []))),
        ]);
        WorldStateId[] states =
        [
            .. Enumerable.Range(0, geometry.Side.Value * geometry.Side.Value * geometry.Side.Value)
                .Select(index => new WorldStateId(checked((uint)(index % 257)))),
        ];
        return CanonicalLogicalProjection.Create(map, [new(Key(17, 2, -3, 4), geometry, states)]);
    }

    private static CanonicalLogicalProjection CreateSparseSchedulesNegativeProjection()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        LogicalSparseInput[] sparse =
        [
            new LogicalSparseInput(1024, ContentKey.Parse("fixture:sign"), new byte[] { 0x00, 0x80, 0xff }),
            new LogicalSparseInput(1, ContentKey.Parse("fixture:chest"), new byte[] { 1, 2, 3, 4 }),
        ];
        LogicalScheduledTick[] ticks =
        [
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Fluid, new WorldTick(9), 3, 50, 17, ContentKey.Parse("fixture:water")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(9), 3, 6, 18, ContentKey.Parse("fixture:later")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(9), -3, 5, 19, ContentKey.Parse("fixture:first")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(2), 0, 4, 20, ContentKey.Parse("fixture:earlier")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(9), -2, 7, 21, ContentKey.Parse("fixture:minus-two")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(9), -1, 8, 22, ContentKey.Parse("fixture:minus-one")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(9), 1, 9, 23, ContentKey.Parse("fixture:one")),
            new LogicalScheduledTick(LogicalScheduledTickQueueKind.Block, new WorldTick(9), 2, 10, 24, ContentKey.Parse("fixture:two")),
        ];
        return CanonicalLogicalProjection.Create(
            Map(0, 2, 7),
            [new(Key(0xfedc_ba98, -2, -3, long.MinValue / 16), geometry, States(geometry, 0, 2, 7), sparse, ticks)]);
    }

    private static CanonicalLogicalProjection CreateMultiRecordSide32Projection()
    {
        SectionGeometry large = SectionGeometry.Side32;
        SectionGeometry small = SectionGeometry.Side16;
        return CanonicalLogicalProjection.Create(
            Map(0, 2),
            [
                new(Key(2, long.MinValue / 32, 0, long.MaxValue / 32), large, States(large, 2, 0)),
                new(Key(2, long.MaxValue / 16, -1, 1), small, States(small, 0, 2)),
            ]);
    }

    private static WorldStateMap Map(params uint[] ids)
    {
        return WorldStateMap.Restore(
        [
            .. ids
                .Append(0U)
                .Distinct()
                .OrderBy(id => id)
                .Select(id => new WorldStateBinding(
                    new WorldStateId(id),
                    id == 0
                        ? CanonicalBlockState.Air
                        : new CanonicalBlockState(ContentKey.Create("fixture", $"state-{id}"), []))),
        ]);
    }

    private static LogicalRecordKey Key(uint dimension, long x, long y, long z)
    {
        return new LogicalRecordKey(LogicalRecordKind.SectionState, new DimensionId(dimension), new SectionCoord(x, y, z));
    }

    private static WorldStateId[] States(SectionGeometry geometry, params uint[] pattern)
    {
        return
        [
            .. Enumerable.Range(0, geometry.Side.Value * geometry.Side.Value * geometry.Side.Value)
                .Select(index => new WorldStateId(pattern[index % pattern.Length])),
        ];
    }

    private static MutableSectionBlockStates CreateWithHistory(SectionGeometry geometry, WorldStateId[] semantic, IEnumerable<int> order)
    {
        MutableSectionBlockStates section = new(geometry, semantic[0], default);
        foreach (int index in order)
        {
            if (semantic[index] != semantic[0])
            {
                Assert.Equal(SectionWriteResult.Changed, section.TrySet(SectionCandidateFixture.ToLocal(index, geometry.Side.Value), semantic[index]));
            }
        }

        return section;
    }

    private static WorldStateId[] Snapshot(MutableSectionBlockStates section)
    {
        SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
        WorldStateId[] states = new WorldStateId[snapshot.Count];
        snapshot.CopyTo(states);
        return states;
    }

    private static string ReadFixture(string fixtureName)
    {
        return File.ReadAllText(Path.Combine(FindFixtureDirectory(), fixtureName)).Trim();
    }

    private static string FindFixtureDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "tests", "fixtures", "g1", "logical-codecs");
            if (Directory.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the logical-codec fixture directory.");
    }

    private static int FirstRecordOffset(byte[] bytes)
    {
        int offset = 18;
        uint mappingCount = ReadUInt32(bytes, 10);
        for (int mappingIndex = 0; mappingIndex < mappingCount; mappingIndex++)
        {
            offset += 4;
            offset = SkipContentKey(bytes, offset);
            ushort properties = ReadUInt16(bytes, offset);
            offset += 2;
            for (int propertyIndex = 0; propertyIndex < properties; propertyIndex++)
            {
                offset = SkipContentKey(bytes, offset);
                offset += 2 + ReadUInt16(bytes, offset);
            }
        }

        return offset;
    }

    private static int NextMappingOffset(byte[] bytes, int offset)
    {
        int cursor = offset + 4;
        cursor = SkipContentKey(bytes, cursor);
        ushort properties = ReadUInt16(bytes, cursor);
        cursor += 2;
        for (int propertyIndex = 0; propertyIndex < properties; propertyIndex++)
        {
            cursor = SkipContentKey(bytes, cursor);
            cursor += 2 + ReadUInt16(bytes, cursor);
        }

        return cursor;
    }

    private static int NextRecordOffset(byte[] bytes, int offset)
    {
        int side = bytes[offset + LogicalRecordKeyCodecV1.EncodedSize];
        int volume = side * side * side;
        int cursor = offset + LogicalRecordKeyCodecV1.EncodedSize + 1;
        ushort palette = ReadUInt16(bytes, cursor);
        cursor += 2 + (palette * 4) + (volume * 2);
        uint sparse = ReadUInt32(bytes, cursor);
        cursor += 4;
        for (int index = 0; index < sparse; index++)
        {
            cursor += 2;
            cursor = SkipContentKey(bytes, cursor);
            cursor += 4 + checked((int)ReadUInt32(bytes, cursor));
        }

        uint schedules = ReadUInt32(bytes, cursor);
        cursor += 4;
        for (int index = 0; index < schedules; index++)
        {
            cursor += 1 + 8 + 1 + 8 + 2;
            cursor = SkipContentKey(bytes, cursor);
        }

        return cursor;
    }

    private static int FirstSparseOffset(byte[] bytes)
    {
        int record = FirstRecordOffset(bytes);
        int side = bytes[record + LogicalRecordKeyCodecV1.EncodedSize];
        int volume = side * side * side;
        int palette = ReadUInt16(bytes, record + LogicalRecordKeyCodecV1.EncodedSize + 1);
        return record + LogicalRecordKeyCodecV1.EncodedSize + 3 + (palette * 4) + (volume * 2);
    }

    private static int NextSparseOffset(byte[] bytes, int offset)
    {
        int cursor = offset + 2;
        cursor = SkipContentKey(bytes, cursor);
        return cursor + 4 + checked((int)ReadUInt32(bytes, cursor));
    }

    private static int FirstScheduleOffset(byte[] bytes)
    {
        int sparse = FirstSparseOffset(bytes);
        int cursor = sparse + 4;
        uint sparseCount = ReadUInt32(bytes, sparse);
        for (int index = 0; index < sparseCount; index++)
        {
            cursor = NextSparseOffset(bytes, cursor);
        }

        return cursor + 4;
    }

    private static int NextScheduleOffset(byte[] bytes, int offset)
    {
        return SkipContentKey(bytes, offset + 1 + 8 + 1 + 8 + 2);
    }

    private static int SkipContentKey(byte[] bytes, int offset)
    {
        return offset + 2 + ReadUInt16(bytes, offset) + 2 + ReadUInt16(bytes, offset + 2 + ReadUInt16(bytes, offset));
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
    }
}
