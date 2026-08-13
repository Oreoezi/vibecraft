using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Time;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Encodes and decodes the storage-neutral G1 canonical logical-projection fixture in one bounded
/// deterministic big-endian version-one representation.
/// </summary>
/// <remarks>
/// Fixture identity <c>VC-G1-E1-LOGICAL-PROJECTION-0.1.0</c>. This explicit codec is not a
/// persistence, database, migration, durable-state, wire-protocol, or user-world format.
/// </remarks>
public static class CanonicalLogicalProjectionCodecV1
{
    /// <summary>The stable identity for this storage-neutral G1 fixture.</summary>
    public const string FixtureId = "VC-G1-E1-LOGICAL-PROJECTION-0.1.0";

    /// <summary>The eight-byte ASCII magic and codec-domain marker.</summary>
    public const string Magic = "VCG1LP01";

    /// <summary>The supported explicit fixture-format version.</summary>
    public const ushort Version = 1;

    /// <summary>The fixed header size in bytes.</summary>
    public const int HeaderSize = 18;

    /// <summary>The largest complete fixture encoding accepted or produced by this codec.</summary>
    public const int MaxEncodedBytes = 64 * 1024 * 1024;

    private const int MinEncodedNamespacedContentIdBytes = 2 + 1 + 2 + 1;
    private const int MinEncodedMappingBytes = sizeof(uint) + MinEncodedNamespacedContentIdBytes + sizeof(ushort);
    private const int MinEncodedSparseBytes = sizeof(ushort) + MinEncodedNamespacedContentIdBytes + sizeof(uint);
    private const int MinEncodedScheduleBytes = sizeof(byte) + sizeof(ulong) + sizeof(byte) + sizeof(ulong) + sizeof(ushort) + MinEncodedNamespacedContentIdBytes;
    private const int MinEncodedSectionBytes = LogicalRecordKeyCodecV1.EncodedSize + sizeof(byte) + sizeof(ushort) + sizeof(uint) + (16 * 16 * 16 * sizeof(ushort)) + sizeof(uint) + sizeof(uint);

    private static ReadOnlySpan<byte> MagicBytes => "VCG1LP01"u8;

    /// <summary>
    /// Encodes a canonical projection into owned immutable bytes and computes its SHA-256 digest.
    /// </summary>
    /// <param name="projection">The validated storage-neutral canonical projection.</param>
    /// <returns>The complete owned fixture encoding and digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a projection violates the codec's bounded canonical contract.</exception>
    public static LogicalProjectionEncoding Encode(CanonicalLogicalProjection projection)
    {
        LogicalProjectionEncodeResult result = TryEncode(projection);
        return result.Succeeded
            ? result.Value
            : throw new InvalidOperationException($"Canonical logical projection encoding failed: {result.Failure!.Code} at byte {result.Failure.ByteOffset}.");
    }

    /// <summary>
    /// Attempts to encode a canonical projection into owned immutable bytes and a SHA-256 digest.
    /// </summary>
    /// <param name="projection">The validated storage-neutral canonical projection.</param>
    /// <returns>The complete owned fixture encoding or a typed failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="projection"/> is <see langword="null"/>.</exception>
    public static LogicalProjectionEncodeResult TryEncode(CanonicalLogicalProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        try
        {
            CanonicalWriter writer = new();
            WriteProjection(projection, writer);
            ImmutableArray<byte> bytes = [.. writer.WrittenSpan];
            return LogicalProjectionEncodeResult.Success(new LogicalProjectionEncoding(bytes));
        }
        catch (CodecWriteException exception)
        {
            return LogicalProjectionEncodeResult.Failed(exception.Failure);
        }
        catch (OverflowException)
        {
            return LogicalProjectionEncodeResult.Failed(LogicalCodecFailure.Create(
                LogicalCodecFailureCode.ArithmeticOverflow,
                0,
                LogicalCodecField.Projection));
        }
    }

    /// <summary>
    /// Attempts to decode one complete strict canonical fixture value without publishing a partial projection.
    /// </summary>
    /// <param name="source">The complete candidate fixture bytes.</param>
    /// <returns>A canonical projection or a typed failure with byte and logical-field metadata.</returns>
    public static LogicalDecodeResult<CanonicalLogicalProjection> TryDecode(ReadOnlySpan<byte> source)
    {
        if (source.Length > MaxEncodedBytes)
        {
            return Failed(LogicalCodecFailureCode.LimitExceeded, MaxEncodedBytes, LogicalCodecField.Projection);
        }

        Reader reader = new(source);
        if (!reader.TryReadBytes(MagicBytes.Length, out ReadOnlySpan<byte> magic))
        {
            return Failed(LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Header);
        }

        if (!magic.SequenceEqual(MagicBytes))
        {
            return Failed(LogicalCodecFailureCode.InvalidHeader, 0, LogicalCodecField.Header);
        }

        if (!reader.TryReadUInt16(out ushort version))
        {
            return Failed(LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Version);
        }

        if (version != Version)
        {
            return Failed(LogicalCodecFailureCode.UnsupportedVersion, MagicBytes.Length, LogicalCodecField.Version);
        }

        if (!reader.TryReadUInt32(out uint mappingCount))
        {
            return Failed(LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Mapping);
        }

        if (mappingCount is 0 or > WorldStateMap.MaxTotalStates)
        {
            return Failed(LogicalCodecFailureCode.LimitExceeded, reader.Offset - sizeof(uint), LogicalCodecField.Mapping);
        }

        if (!reader.TryReadUInt32(out uint recordCount))
        {
            return Failed(LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Record);
        }

        if (recordCount > CanonicalLogicalProjection.MaxSectionRecords)
        {
            return Failed(LogicalCodecFailureCode.LimitExceeded, reader.Offset - sizeof(uint), LogicalCodecField.Record);
        }

        long minimumMappingBytes = (long)mappingCount * MinEncodedMappingBytes;
        long minimumRecordBytes = (long)recordCount * MinEncodedSectionBytes;
        if (minimumMappingBytes + minimumRecordBytes > reader.Remaining)
        {
            LogicalCodecField field = minimumMappingBytes > reader.Remaining
                ? LogicalCodecField.Mapping
                : LogicalCodecField.Record;
            return Failed(LogicalCodecFailureCode.IncorrectLength, reader.Offset, field);
        }

        if (!TryReadMapping(ref reader, checked((int)mappingCount), out WorldStateMap? map, out LogicalCodecFailure? mappingFailure))
        {
            return LogicalDecodeResult<CanonicalLogicalProjection>.Failed(mappingFailure!);
        }

        List<LogicalSectionInput> sections = new(checked((int)recordCount));
        LogicalRecordKey? previousKey = null;
        for (int recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            if (!TryReadSection(ref reader, map!, previousKey, recordIndex, out LogicalSectionInput? section, out LogicalRecordKey key, out LogicalCodecFailure? failure))
            {
                return LogicalDecodeResult<CanonicalLogicalProjection>.Failed(failure!);
            }

            previousKey = key;
            sections.Add(section!);
        }

        if (!reader.AtEnd)
        {
            return Failed(LogicalCodecFailureCode.TrailingData, reader.Offset, LogicalCodecField.Projection);
        }

        try
        {
            return LogicalDecodeResult<CanonicalLogicalProjection>.Success(CanonicalLogicalProjection.Create(map!, sections));
        }
        catch (OverflowException)
        {
            return Failed(LogicalCodecFailureCode.ArithmeticOverflow, reader.Offset, LogicalCodecField.Record);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failed(LogicalCodecFailureCode.LimitExceeded, reader.Offset, LogicalCodecField.Projection);
        }
        catch (ArgumentException)
        {
            return Failed(LogicalCodecFailureCode.InvalidValue, reader.Offset, LogicalCodecField.Projection);
        }
    }

    private static void WriteProjection(CanonicalLogicalProjection projection, CanonicalWriter writer)
    {
        if (projection.MappingBindings.IsDefault || projection.MappingBindings.Length is 0 or > WorldStateMap.MaxTotalStates)
        {
            throw InvalidWrite(LogicalCodecFailureCode.LimitExceeded, writer.Offset, LogicalCodecField.Mapping);
        }

        if (projection.Sections.IsDefault || projection.Sections.Length > CanonicalLogicalProjection.MaxSectionRecords)
        {
            throw InvalidWrite(LogicalCodecFailureCode.LimitExceeded, writer.Offset, LogicalCodecField.Record);
        }

        writer.WriteBytes(MagicBytes);
        writer.WriteUInt16(Version);
        writer.WriteUInt32(checked((uint)projection.MappingBindings.Length));
        writer.WriteUInt32(checked((uint)projection.Sections.Length));

        BlockStateId? previousId = null;
        HashSet<CanonicalBlockState> mappedStates = [];
        HashSet<uint> mappedIds = [];
        foreach (WorldStateBinding binding in projection.MappingBindings)
        {
            if (binding.State is null || (previousId.HasValue && binding.Id.Value <= previousId.Value.Value) || !mappedStates.Add(binding.State))
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalOrder, writer.Offset, LogicalCodecField.Mapping);
            }

            previousId = binding.Id;
            _ = mappedIds.Add(binding.Id.Value);
            writer.WriteUInt32(binding.Id.Value);
            WriteBlockState(writer, binding.State);
        }

        if (projection.MappingBindings[0].Id.Value != 0 || !projection.MappingBindings[0].State.Equals(CanonicalBlockState.Air))
        {
            throw InvalidWrite(LogicalCodecFailureCode.InvalidValue, HeaderSize, LogicalCodecField.Mapping);
        }

        LogicalRecordKey? previousKey = null;
        foreach (LogicalSectionRecord section in projection.Sections)
        {
            if (section is null || (previousKey.HasValue && LogicalRecordKeyComparer.Instance.Compare(previousKey.Value, section.Key) >= 0))
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalOrder, writer.Offset, LogicalCodecField.Record);
            }

            previousKey = section.Key;
            WriteSection(writer, section, mappedIds);
        }
    }

    private static void WriteBlockState(CanonicalWriter writer, CanonicalBlockState state)
    {
        WriteNamespacedContentId(writer, state.Block, LogicalCodecField.NamespacedContentId);
        if (state.Properties.IsDefault || state.Properties.Length > CanonicalBlockState.MaxProperties)
        {
            throw InvalidWrite(LogicalCodecFailureCode.LimitExceeded, writer.Offset, LogicalCodecField.Property);
        }

        writer.WriteUInt16(checked((ushort)state.Properties.Length));
        NamespacedContentId? previous = null;
        foreach (BlockStateProperty property in state.Properties)
        {
            if (!property.IsValid || (previous.HasValue && previous.Value.CompareTo(property.Key) >= 0))
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalOrder, writer.Offset, LogicalCodecField.Property);
            }

            previous = property.Key;
            WriteNamespacedContentId(writer, property.Key, LogicalCodecField.Property);
            writer.WriteAscii(property.Value, BlockStateProperty.MaxValueLength, LogicalCodecField.Property);
        }
    }

    private static void WriteSection(
        CanonicalWriter writer,
        LogicalSectionRecord section,
        HashSet<uint> mappedIds)
    {
        Span<byte> encodedKey = stackalloc byte[LogicalRecordKeyCodecV1.EncodedSize];
        LogicalEncodeResult keyResult = LogicalRecordKeyCodecV1.TryEncode(section.Key, encodedKey);
        if (!keyResult.Succeeded)
        {
            throw new CodecWriteException(keyResult.Failure!);
        }

        writer.WriteBytes(encodedKey);
        int side = section.Geometry.Side.Value;
        if (side is not (16 or 32))
        {
            throw InvalidWrite(LogicalCodecFailureCode.InvalidValue, writer.Offset, LogicalCodecField.Side);
        }

        int volume = checked(side * side * side);
        if (section.States.IsDefault || section.States.Length != volume ||
            section.Palette.IsDefault || section.Palette.Length is 0 or > ushort.MaxValue ||
            section.PaletteIndices.IsDefault || section.PaletteIndices.Length != volume ||
            section.SparseRecords.IsDefault || section.SparseRecords.Length > volume ||
            section.ScheduledTicks.IsDefault || section.ScheduledTicks.Length > LogicalScheduledTick.MaxTicksPerSection)
        {
            throw InvalidWrite(LogicalCodecFailureCode.InvalidValue, writer.Offset, LogicalCodecField.Record);
        }

        writer.WriteByte(checked((byte)side));
        writer.WriteUInt16(checked((ushort)section.Palette.Length));
        BlockStateId? previousPalette = null;
        foreach (BlockStateId paletteEntry in section.Palette)
        {
            if (!mappedIds.Contains(paletteEntry.Value) || (previousPalette.HasValue && paletteEntry.Value <= previousPalette.Value.Value))
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalPalette, writer.Offset, LogicalCodecField.Palette);
            }

            previousPalette = paletteEntry;
            writer.WriteUInt32(paletteEntry.Value);
        }

        bool[] usedPalette = new bool[section.Palette.Length];
        for (int stateIndex = 0; stateIndex < section.PaletteIndices.Length; stateIndex++)
        {
            ushort paletteIndex = section.PaletteIndices[stateIndex];
            if (paletteIndex >= section.Palette.Length || section.Palette[paletteIndex] != section.States[stateIndex])
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalPalette, writer.Offset, LogicalCodecField.Voxel);
            }

            usedPalette[paletteIndex] = true;
            writer.WriteUInt16(paletteIndex);
        }

        if (usedPalette.Any(used => !used))
        {
            throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalPalette, writer.Offset, LogicalCodecField.Palette);
        }

        writer.WriteUInt32(checked((uint)section.SparseRecords.Length));
        int previousSparseIndex = -1;
        foreach (LogicalSparseRecord sparse in section.SparseRecords)
        {
            if (sparse is null || sparse.LocalIndex.Value <= previousSparseIndex || sparse.LocalIndex.Value >= volume ||
                !sparse.Type.IsValid || sparse.Payload.IsDefault || sparse.Payload.Length > LogicalSparseRecord.MaxPayloadBytes)
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalOrder, writer.Offset, LogicalCodecField.Sparse);
            }

            previousSparseIndex = sparse.LocalIndex.Value;
            writer.WriteUInt16(checked((ushort)sparse.LocalIndex.Value));
            WriteNamespacedContentId(writer, sparse.Type, LogicalCodecField.NamespacedContentId);
            writer.WriteUInt32(checked((uint)sparse.Payload.Length));
            writer.WriteBytes(sparse.Payload.AsSpan());
        }

        writer.WriteUInt32(checked((uint)section.ScheduledTicks.Length));
        LogicalScheduledTick? previousTick = null;
        HashSet<ulong> sequences = [];
        HashSet<ScheduledTickIdentity> coalescing = [];
        foreach (LogicalScheduledTick tick in section.ScheduledTicks)
        {
            if (!Enum.IsDefined(tick.Queue) || tick.Priority is < LogicalScheduledTick.MinimumPriority or > LogicalScheduledTick.MaximumPriority ||
                tick.LocalIndex.Value >= volume || !tick.ExpectedType.IsValid || !sequences.Add(tick.Sequence) ||
                !coalescing.Add(new ScheduledTickIdentity(tick.Queue, tick.LocalIndex, tick.ExpectedType)) ||
                (previousTick.HasValue && CompareTicks(previousTick.Value, tick) >= 0))
            {
                throw InvalidWrite(LogicalCodecFailureCode.NonCanonicalOrder, writer.Offset, LogicalCodecField.Schedule);
            }

            previousTick = tick;
            writer.WriteByte((byte)tick.Queue);
            writer.WriteUInt64(tick.DueTick.Value);
            writer.WriteByte(unchecked((byte)tick.Priority));
            writer.WriteUInt64(tick.Sequence);
            writer.WriteUInt16(checked((ushort)tick.LocalIndex.Value));
            WriteNamespacedContentId(writer, tick.ExpectedType, LogicalCodecField.ExpectedType);
        }
    }

    private static bool TryReadMapping(
        ref Reader reader,
        int mappingCount,
        out WorldStateMap? mapping,
        out LogicalCodecFailure? failure)
    {
        mapping = null;
        List<WorldStateBinding> bindings = new(mappingCount);
        HashSet<CanonicalBlockState> states = [];
        uint previousId = 0;
        for (int mappingIndex = 0; mappingIndex < mappingCount; mappingIndex++)
        {
            int idOffset = reader.Offset;
            if (!reader.TryReadUInt32(out uint id))
            {
                return FailRead(ref mapping, out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Mapping, -1, mappingIndex);
            }

            if (mappingIndex > 0 && id <= previousId)
            {
                LogicalCodecFailureCode code = id == previousId ? LogicalCodecFailureCode.DuplicateIdentity : LogicalCodecFailureCode.NonCanonicalOrder;
                return FailRead(ref mapping, out failure, code, idOffset, LogicalCodecField.Mapping, -1, mappingIndex);
            }

            previousId = id;
            if (!TryReadNamespacedContentId(ref reader, LogicalCodecField.NamespacedContentId, -1, mappingIndex, out NamespacedContentId block, out failure))
            {
                mapping = null;
                return false;
            }

            int propertyCountOffset = reader.Offset;
            if (!reader.TryReadUInt16(out ushort propertyCount))
            {
                return FailRead(ref mapping, out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Property, -1, mappingIndex);
            }

            if (propertyCount > CanonicalBlockState.MaxProperties)
            {
                return FailRead(ref mapping, out failure, LogicalCodecFailureCode.LimitExceeded, propertyCountOffset, LogicalCodecField.Property, -1, mappingIndex);
            }

            if ((long)propertyCount * (MinEncodedNamespacedContentIdBytes + 3) > reader.Remaining)
            {
                return FailRead(ref mapping, out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Property, -1, mappingIndex);
            }

            List<BlockStateProperty> properties = new(propertyCount);
            NamespacedContentId? previousKey = null;
            for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
            {
                int keyOffset = reader.Offset;
                if (!TryReadNamespacedContentId(ref reader, LogicalCodecField.Property, -1, propertyIndex, out NamespacedContentId key, out failure))
                {
                    mapping = null;
                    return false;
                }

                if (previousKey.HasValue && previousKey.Value.CompareTo(key) >= 0)
                {
                    LogicalCodecFailureCode code = previousKey.Value.Equals(key) ? LogicalCodecFailureCode.DuplicateIdentity : LogicalCodecFailureCode.NonCanonicalOrder;
                    return FailRead(ref mapping, out failure, code, keyOffset, LogicalCodecField.Property, -1, propertyIndex);
                }

                previousKey = key;
                if (!TryReadAscii(ref reader, BlockStateProperty.MaxValueLength, LogicalCodecField.Property, -1, propertyIndex, out string value, out failure))
                {
                    mapping = null;
                    return false;
                }

                try
                {
                    properties.Add(BlockStateProperty.Create(key, value));
                }
                catch (ArgumentException)
                {
                    return FailRead(ref mapping, out failure, LogicalCodecFailureCode.InvalidText, reader.Offset - value.Length, LogicalCodecField.Property, -1, propertyIndex);
                }
            }

            CanonicalBlockState state;
            try
            {
                state = new CanonicalBlockState(block, properties);
            }
            catch (ArgumentException)
            {
                return FailRead(ref mapping, out failure, LogicalCodecFailureCode.InvalidValue, idOffset, LogicalCodecField.Mapping, -1, mappingIndex);
            }

            if (!states.Add(state))
            {
                return FailRead(ref mapping, out failure, LogicalCodecFailureCode.DuplicateIdentity, idOffset, LogicalCodecField.Mapping, -1, mappingIndex);
            }

            bindings.Add(new WorldStateBinding(new BlockStateId(id), state));
        }

        try
        {
            mapping = WorldStateMap.Restore(bindings);
            failure = null;
            return true;
        }
        catch (ArgumentException)
        {
            return FailRead(ref mapping, out failure, LogicalCodecFailureCode.InvalidValue, HeaderSize, LogicalCodecField.Mapping);
        }
    }

    private static bool TryReadSection(
        ref Reader reader,
        WorldStateMap mapping,
        LogicalRecordKey? previousKey,
        int recordIndex,
        out LogicalSectionInput? section,
        out LogicalRecordKey key,
        out LogicalCodecFailure? failure)
    {
        section = null;
        key = default;
        int keyOffset = reader.Offset;
        if (!reader.TryReadBytes(LogicalRecordKeyCodecV1.EncodedSize, out ReadOnlySpan<byte> encodedKey))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.RecordKey, recordIndex);
        }

        LogicalDecodeResult<LogicalRecordKey> keyResult = LogicalRecordKeyCodecV1.TryDecode(encodedKey);
        if (!keyResult.Succeeded)
        {
            LogicalCodecFailure underlying = keyResult.Failure!;
            return FailRead(out failure, underlying.Code, keyOffset + underlying.ByteOffset, underlying.Field, recordIndex);
        }

        key = keyResult.Value;
        if (previousKey.HasValue && LogicalRecordKeyComparer.Instance.Compare(previousKey.Value, key) >= 0)
        {
            LogicalCodecFailureCode code = previousKey.Value == key ? LogicalCodecFailureCode.DuplicateIdentity : LogicalCodecFailureCode.NonCanonicalOrder;
            return FailRead(out failure, code, keyOffset, LogicalCodecField.RecordKey, recordIndex);
        }

        int sideOffset = reader.Offset;
        if (!reader.TryReadByte(out byte sideByte))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Side, recordIndex);
        }

        if (sideByte is not (16 or 32))
        {
            return FailRead(out failure, LogicalCodecFailureCode.InvalidValue, sideOffset, LogicalCodecField.Side, recordIndex);
        }

        SectionGeometry geometry = new(new SectionSide(sideByte));
        try
        {
            _ = geometry.GetOrigin(key.Coordinate);
            _ = geometry.GetEndInclusive(key.Coordinate);
        }
        catch (OverflowException)
        {
            return FailRead(out failure, LogicalCodecFailureCode.ArithmeticOverflow, keyOffset, LogicalCodecField.RecordKey, recordIndex);
        }

        int volume = checked(sideByte * sideByte * sideByte);
        int paletteCountOffset = reader.Offset;
        if (!reader.TryReadUInt16(out ushort paletteCount))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Palette, recordIndex);
        }

        if (paletteCount is 0 or > 32_768 || paletteCount > volume)
        {
            return FailRead(out failure, LogicalCodecFailureCode.NonCanonicalPalette, paletteCountOffset, LogicalCodecField.Palette, recordIndex);
        }

        long minimumSectionRemainder = ((long)paletteCount * sizeof(uint)) + ((long)volume * sizeof(ushort)) + sizeof(uint) + sizeof(uint);
        if (minimumSectionRemainder > reader.Remaining)
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Palette, recordIndex);
        }

        List<BlockStateId> palette = new(paletteCount);
        uint previousStateId = 0;
        for (int paletteIndex = 0; paletteIndex < paletteCount; paletteIndex++)
        {
            int paletteOffset = reader.Offset;
            if (!reader.TryReadUInt32(out uint stateId))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Palette, recordIndex, paletteIndex);
            }

            if (paletteIndex > 0 && stateId <= previousStateId)
            {
                LogicalCodecFailureCode code = stateId == previousStateId ? LogicalCodecFailureCode.DuplicateIdentity : LogicalCodecFailureCode.NonCanonicalPalette;
                return FailRead(out failure, code, paletteOffset, LogicalCodecField.Palette, recordIndex, paletteIndex);
            }

            BlockStateId value = new(stateId);
            if (!mapping.TryGetState(value, out _))
            {
                return FailRead(out failure, LogicalCodecFailureCode.UnmappedBlockState, paletteOffset, LogicalCodecField.Palette, recordIndex, paletteIndex);
            }

            previousStateId = stateId;
            palette.Add(value);
        }

        List<BlockStateId> states = new(volume);
        bool[] usedPalette = new bool[paletteCount];
        for (int voxelIndex = 0; voxelIndex < volume; voxelIndex++)
        {
            int voxelOffset = reader.Offset;
            if (!reader.TryReadUInt16(out ushort paletteIndex))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Voxel, recordIndex, voxelIndex);
            }

            if (paletteIndex >= paletteCount)
            {
                return FailRead(out failure, LogicalCodecFailureCode.IndexOutOfRange, voxelOffset, LogicalCodecField.Voxel, recordIndex, voxelIndex);
            }

            usedPalette[paletteIndex] = true;
            states.Add(palette[paletteIndex]);
        }

        if (usedPalette.Any(used => !used))
        {
            return FailRead(out failure, LogicalCodecFailureCode.NonCanonicalPalette, paletteCountOffset, LogicalCodecField.Palette, recordIndex);
        }

        if (!TryReadSparse(ref reader, volume, recordIndex, out List<LogicalSparseInput>? sparse, out failure) ||
            !TryReadSchedules(ref reader, volume, recordIndex, out List<LogicalScheduledTick>? schedules, out failure))
        {
            return false;
        }

        try
        {
            section = new LogicalSectionInput(key, geometry, states, sparse!, schedules!);
            return true;
        }
        catch (OverflowException)
        {
            return FailRead(out failure, LogicalCodecFailureCode.ArithmeticOverflow, keyOffset, LogicalCodecField.RecordKey, recordIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            return FailRead(out failure, LogicalCodecFailureCode.LimitExceeded, keyOffset, LogicalCodecField.Record, recordIndex);
        }
        catch (ArgumentException)
        {
            return FailRead(out failure, LogicalCodecFailureCode.InvalidValue, keyOffset, LogicalCodecField.Record, recordIndex);
        }
    }

    private static bool TryReadSparse(
        ref Reader reader,
        int volume,
        int recordIndex,
        out List<LogicalSparseInput>? sparse,
        out LogicalCodecFailure? failure)
    {
        sparse = null;
        int countOffset = reader.Offset;
        if (!reader.TryReadUInt32(out uint sparseCount))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Sparse, recordIndex);
        }

        if (sparseCount > volume)
        {
            return FailRead(out failure, LogicalCodecFailureCode.LimitExceeded, countOffset, LogicalCodecField.Sparse, recordIndex);
        }

        if (((long)sparseCount * MinEncodedSparseBytes) + sizeof(uint) > reader.Remaining)
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Sparse, recordIndex);
        }

        sparse = new List<LogicalSparseInput>(checked((int)sparseCount));
        int previousIndex = -1;
        for (int sparseIndex = 0; sparseIndex < sparseCount; sparseIndex++)
        {
            int localIndexOffset = reader.Offset;
            if (!reader.TryReadUInt16(out ushort localIndex))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.LocalIndex, recordIndex, sparseIndex);
            }

            if (localIndex <= previousIndex)
            {
                LogicalCodecFailureCode code = localIndex == previousIndex ? LogicalCodecFailureCode.DuplicateIdentity : LogicalCodecFailureCode.NonCanonicalOrder;
                return FailRead(out failure, code, localIndexOffset, LogicalCodecField.Sparse, recordIndex, sparseIndex);
            }

            if (localIndex >= volume)
            {
                return FailRead(out failure, LogicalCodecFailureCode.IndexOutOfRange, localIndexOffset, LogicalCodecField.LocalIndex, recordIndex, sparseIndex);
            }

            previousIndex = localIndex;
            if (!TryReadNamespacedContentId(ref reader, LogicalCodecField.NamespacedContentId, recordIndex, sparseIndex, out NamespacedContentId type, out failure))
            {
                return false;
            }

            int payloadLengthOffset = reader.Offset;
            if (!reader.TryReadUInt32(out uint payloadLength))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Payload, recordIndex, sparseIndex);
            }

            if (payloadLength > LogicalSparseRecord.MaxPayloadBytes)
            {
                return FailRead(out failure, LogicalCodecFailureCode.LimitExceeded, payloadLengthOffset, LogicalCodecField.Payload, recordIndex, sparseIndex);
            }

            if (!reader.TryReadBytes(checked((int)payloadLength), out ReadOnlySpan<byte> payload))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Payload, recordIndex, sparseIndex);
            }

            sparse.Add(LogicalSparseInput.FromEncoded(new LocalIndex(localIndex), type, payload));
        }

        failure = null;
        return true;
    }

    private static bool TryReadSchedules(
        ref Reader reader,
        int volume,
        int recordIndex,
        out List<LogicalScheduledTick>? schedules,
        out LogicalCodecFailure? failure)
    {
        schedules = null;
        int countOffset = reader.Offset;
        if (!reader.TryReadUInt32(out uint scheduleCount))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Schedule, recordIndex);
        }

        if (scheduleCount > LogicalScheduledTick.MaxTicksPerSection)
        {
            return FailRead(out failure, LogicalCodecFailureCode.LimitExceeded, countOffset, LogicalCodecField.Schedule, recordIndex);
        }

        if ((long)scheduleCount * MinEncodedScheduleBytes > reader.Remaining)
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Schedule, recordIndex);
        }

        schedules = new List<LogicalScheduledTick>(checked((int)scheduleCount));
        LogicalScheduledTick? previous = null;
        HashSet<ulong> sequences = [];
        HashSet<ScheduledTickIdentity> coalescing = [];
        for (int scheduleIndex = 0; scheduleIndex < scheduleCount; scheduleIndex++)
        {
            int queueOffset = reader.Offset;
            if (!reader.TryReadByte(out byte queueByte))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Queue, recordIndex, scheduleIndex);
            }

            if (queueByte is not ((byte)LogicalScheduledTickQueueKind.Block or (byte)LogicalScheduledTickQueueKind.Fluid))
            {
                return FailRead(out failure, LogicalCodecFailureCode.InvalidEnum, queueOffset, LogicalCodecField.Queue, recordIndex, scheduleIndex);
            }

            if (!reader.TryReadUInt64(out ulong dueTick))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.DueTick, recordIndex, scheduleIndex);
            }

            int priorityOffset = reader.Offset;
            if (!reader.TryReadByte(out byte priorityByte))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Priority, recordIndex, scheduleIndex);
            }

            int priority = unchecked((sbyte)priorityByte);
            if (priority is < LogicalScheduledTick.MinimumPriority or > LogicalScheduledTick.MaximumPriority)
            {
                return FailRead(out failure, LogicalCodecFailureCode.InvalidValue, priorityOffset, LogicalCodecField.Priority, recordIndex, scheduleIndex);
            }

            if (!reader.TryReadUInt64(out ulong sequence))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.Sequence, recordIndex, scheduleIndex);
            }

            int localIndexOffset = reader.Offset;
            if (!reader.TryReadUInt16(out ushort localIndex))
            {
                return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, LogicalCodecField.LocalIndex, recordIndex, scheduleIndex);
            }

            if (localIndex >= volume)
            {
                return FailRead(out failure, LogicalCodecFailureCode.IndexOutOfRange, localIndexOffset, LogicalCodecField.LocalIndex, recordIndex, scheduleIndex);
            }

            if (!TryReadNamespacedContentId(ref reader, LogicalCodecField.ExpectedType, recordIndex, scheduleIndex, out NamespacedContentId expectedType, out failure))
            {
                return false;
            }

            if (!sequences.Add(sequence))
            {
                return FailRead(out failure, LogicalCodecFailureCode.DuplicateIdentity, queueOffset, LogicalCodecField.Sequence, recordIndex, scheduleIndex);
            }

            LogicalScheduledTickQueueKind queue = (LogicalScheduledTickQueueKind)queueByte;
            LocalIndex typedLocalIndex = new(localIndex);
            if (!coalescing.Add(new ScheduledTickIdentity(queue, typedLocalIndex, expectedType)))
            {
                return FailRead(out failure, LogicalCodecFailureCode.DuplicateIdentity, queueOffset, LogicalCodecField.Schedule, recordIndex, scheduleIndex);
            }

            LogicalScheduledTick tick = new(queue, new WorldTick(dueTick), priority, sequence, typedLocalIndex, expectedType);
            if (previous.HasValue && CompareTicks(previous.Value, tick) >= 0)
            {
                return FailRead(out failure, LogicalCodecFailureCode.NonCanonicalOrder, queueOffset, LogicalCodecField.Schedule, recordIndex, scheduleIndex);
            }

            previous = tick;
            schedules.Add(tick);
        }

        failure = null;
        return true;
    }

    private static bool TryReadNamespacedContentId(
        ref Reader reader,
        LogicalCodecField field,
        int recordIndex,
        int elementIndex,
        out NamespacedContentId key,
        out LogicalCodecFailure? failure)
    {
        key = default;
        int offset = reader.Offset;
        if (!TryReadAscii(ref reader, NamespacedContentId.MaxNamespaceLength, field, recordIndex, elementIndex, out string @namespace, out failure) ||
            !TryReadAscii(ref reader, NamespacedContentId.MaxPathLength, field, recordIndex, elementIndex, out string path, out failure))
        {
            return false;
        }

        try
        {
            key = NamespacedContentId.Create(@namespace, path);
            return true;
        }
        catch (ArgumentException)
        {
            return FailRead(out failure, LogicalCodecFailureCode.InvalidText, offset, field, recordIndex, elementIndex);
        }
    }

    private static bool TryReadAscii(
        ref Reader reader,
        int maximumLength,
        LogicalCodecField field,
        int recordIndex,
        int elementIndex,
        out string value,
        out LogicalCodecFailure? failure)
    {
        value = string.Empty;
        int lengthOffset = reader.Offset;
        if (!reader.TryReadUInt16(out ushort length))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, field, recordIndex, elementIndex);
        }

        if (length == 0 || length > maximumLength)
        {
            return FailRead(out failure, LogicalCodecFailureCode.InvalidText, lengthOffset, field, recordIndex, elementIndex);
        }

        int valueOffset = reader.Offset;
        if (!reader.TryReadBytes(length, out ReadOnlySpan<byte> bytes))
        {
            return FailRead(out failure, LogicalCodecFailureCode.IncorrectLength, reader.Offset, field, recordIndex, elementIndex);
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] > 0x7f)
            {
                return FailRead(out failure, LogicalCodecFailureCode.InvalidText, valueOffset + index, field, recordIndex, elementIndex);
            }
        }

        value = Encoding.ASCII.GetString(bytes);
        failure = null;
        return true;
    }

    private static void WriteNamespacedContentId(CanonicalWriter writer, NamespacedContentId key, LogicalCodecField field)
    {
        if (!key.IsValid)
        {
            throw InvalidWrite(LogicalCodecFailureCode.InvalidText, writer.Offset, field);
        }

        writer.WriteAscii(key.Namespace, NamespacedContentId.MaxNamespaceLength, field);
        writer.WriteAscii(key.Path, NamespacedContentId.MaxPathLength, field);
    }

    private static int CompareTicks(LogicalScheduledTick left, LogicalScheduledTick right)
    {
        int queue = left.Queue.CompareTo(right.Queue);
        if (queue != 0)
        {
            return queue;
        }

        int dueTick = left.DueTick.Value.CompareTo(right.DueTick.Value);
        if (dueTick != 0)
        {
            return dueTick;
        }

        int priority = left.Priority.CompareTo(right.Priority);
        return priority != 0 ? priority : left.Sequence.CompareTo(right.Sequence);
    }

    private static CodecWriteException InvalidWrite(LogicalCodecFailureCode code, int offset, LogicalCodecField field)
    {
        return new CodecWriteException(LogicalCodecFailure.Create(code, offset, field));
    }

    private static LogicalDecodeResult<CanonicalLogicalProjection> Failed(LogicalCodecFailureCode code, int offset, LogicalCodecField field, int recordIndex = -1, int elementIndex = -1)
    {
        return LogicalDecodeResult<CanonicalLogicalProjection>.Failed(LogicalCodecFailure.Create(code, offset, field, recordIndex, elementIndex));
    }

    private static bool FailRead(out LogicalCodecFailure? failure, LogicalCodecFailureCode code, int offset, LogicalCodecField field, int recordIndex = -1, int elementIndex = -1)
    {
        failure = LogicalCodecFailure.Create(code, offset, field, recordIndex, elementIndex);
        return false;
    }

    private static bool FailRead(ref WorldStateMap? mapping, out LogicalCodecFailure? failure, LogicalCodecFailureCode code, int offset, LogicalCodecField field, int recordIndex = -1, int elementIndex = -1)
    {
        mapping = null;
        return FailRead(out failure, code, offset, field, recordIndex, elementIndex);
    }

    private readonly record struct ScheduledTickIdentity(
        LogicalScheduledTickQueueKind Queue,
        LocalIndex LocalIndex,
        NamespacedContentId ExpectedType);

    private sealed class CodecWriteException(LogicalCodecFailure failure) : Exception
    {
        public LogicalCodecFailure Failure { get; } = failure;
    }

    private sealed class CanonicalWriter
    {
        private readonly ArrayBufferWriter<byte> buffer = new();

        public int Offset => buffer.WrittenCount;

        public ReadOnlySpan<byte> WrittenSpan => buffer.WrittenSpan;

        public void WriteByte(byte value)
        {
            EnsureAvailable(sizeof(byte), LogicalCodecField.Projection);
            Span<byte> destination = buffer.GetSpan(sizeof(byte));
            destination[0] = value;
            buffer.Advance(sizeof(byte));
        }

        public void WriteUInt16(ushort value)
        {
            EnsureAvailable(sizeof(ushort), LogicalCodecField.Projection);
            Span<byte> destination = buffer.GetSpan(sizeof(ushort));
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
            buffer.Advance(sizeof(ushort));
        }

        public void WriteUInt32(uint value)
        {
            EnsureAvailable(sizeof(uint), LogicalCodecField.Projection);
            Span<byte> destination = buffer.GetSpan(sizeof(uint));
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
            buffer.Advance(sizeof(uint));
        }

        public void WriteUInt64(ulong value)
        {
            EnsureAvailable(sizeof(ulong), LogicalCodecField.Projection);
            Span<byte> destination = buffer.GetSpan(sizeof(ulong));
            BinaryPrimitives.WriteUInt64BigEndian(destination, value);
            buffer.Advance(sizeof(ulong));
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            EnsureAvailable(value.Length, LogicalCodecField.Projection);
            value.CopyTo(buffer.GetSpan(value.Length));
            buffer.Advance(value.Length);
        }

        public void WriteAscii(string value, int maximumLength, LogicalCodecField field)
        {
            if (value.Length == 0 || value.Length > maximumLength || value.Any(character => character > 0x7f))
            {
                throw InvalidWrite(LogicalCodecFailureCode.InvalidText, Offset, field);
            }

            WriteUInt16(checked((ushort)value.Length));
            EnsureAvailable(value.Length, field);
            Span<byte> destination = buffer.GetSpan(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                destination[index] = checked((byte)value[index]);
            }

            buffer.Advance(value.Length);
        }

        private void EnsureAvailable(int count, LogicalCodecField field)
        {
            if (count < 0 || count > MaxEncodedBytes - Offset)
            {
                throw InvalidWrite(LogicalCodecFailureCode.LimitExceeded, Offset, field);
            }
        }
    }

    private ref struct Reader
    {
        private ReadOnlySpan<byte> Source { get; }

        private int OffsetInternal { get; set; }

        public Reader(ReadOnlySpan<byte> source)
        {
            Source = source;
            OffsetInternal = 0;
        }

        public readonly int Offset => OffsetInternal;

        public readonly bool AtEnd => OffsetInternal == Source.Length;

        public readonly int Remaining => Source.Length - OffsetInternal;

        public bool TryReadByte(out byte value)
        {
            if (!TryReadBytes(sizeof(byte), out ReadOnlySpan<byte> bytes))
            {
                value = default;
                return false;
            }

            value = bytes[0];
            return true;
        }

        public bool TryReadUInt16(out ushort value)
        {
            if (!TryReadBytes(sizeof(ushort), out ReadOnlySpan<byte> bytes))
            {
                value = default;
                return false;
            }

            value = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            return true;
        }

        public bool TryReadUInt32(out uint value)
        {
            if (!TryReadBytes(sizeof(uint), out ReadOnlySpan<byte> bytes))
            {
                value = default;
                return false;
            }

            value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            return true;
        }

        public bool TryReadUInt64(out ulong value)
        {
            if (!TryReadBytes(sizeof(ulong), out ReadOnlySpan<byte> bytes))
            {
                value = default;
                return false;
            }

            value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
            return true;
        }

        public bool TryReadBytes(int count, out ReadOnlySpan<byte> value)
        {
            if (count < 0 || count > Source.Length - OffsetInternal)
            {
                value = default;
                OffsetInternal = Source.Length;
                return false;
            }

            value = Source.Slice(OffsetInternal, count);
            OffsetInternal += count;
            return true;
        }
    }
}
