using System.Buffers.Binary;
using VibeCraft.Primitives.Coordinates;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Encodes the storage-neutral G1 logical record-key fixture in one canonical 30-byte form.
/// </summary>
/// <remarks>
/// This codec exists for deterministic logical identity and golden fixtures. Its bytes are not a
/// user-world format, database row/key layout, persistence envelope, or wire-protocol message.
/// </remarks>
public static class LogicalRecordKeyCodecV1
{
    /// <summary>The exact encoded size of one logical record key.</summary>
    public const int EncodedSize = 30;

    /// <summary>The byte offset of the unsigned 16-bit record kind.</summary>
    public const int RecordKindOffset = 0;

    /// <summary>The encoded size of the record-kind field.</summary>
    public const int RecordKindSize = sizeof(ushort);

    /// <summary>The byte offset of the unsigned 32-bit dimension identity.</summary>
    public const int DimensionOffset = RecordKindOffset + RecordKindSize;

    /// <summary>The encoded size of the dimension field.</summary>
    public const int DimensionSize = sizeof(uint);

    /// <summary>The byte offset of the sign-biased signed 64-bit X coordinate.</summary>
    public const int SectionXOffset = DimensionOffset + DimensionSize;

    /// <summary>The encoded size of each section-coordinate field.</summary>
    public const int SectionCoordinateSize = sizeof(ulong);

    /// <summary>The byte offset of the sign-biased signed 64-bit Y coordinate.</summary>
    public const int SectionYOffset = SectionXOffset + SectionCoordinateSize;

    /// <summary>The byte offset of the sign-biased signed 64-bit Z coordinate.</summary>
    public const int SectionZOffset = SectionYOffset + SectionCoordinateSize;

    private const ulong SignedOrderBias = 0x8000_0000_0000_0000;

    /// <summary>
    /// Attempts to encode a key into an exactly sized destination without partial writes.
    /// </summary>
    /// <param name="key">The typed logical record key.</param>
    /// <param name="destination">An exactly <see cref="EncodedSize"/>-byte destination.</param>
    /// <returns>An immutable success result or a typed validation failure.</returns>
    public static LogicalEncodeResult TryEncode(LogicalRecordKey key, Span<byte> destination)
    {
        if (destination.Length != EncodedSize)
        {
            return LogicalEncodeResult.Failed(CreateLengthFailure(destination.Length));
        }

        if (!IsSupported(key.Kind))
        {
            return LogicalEncodeResult.Failed(LogicalCodecFailure.Create(
                LogicalCodecFailureCode.UnknownRecordKind,
                RecordKindOffset,
                LogicalCodecField.RecordKind));
        }

        Span<byte> staged = stackalloc byte[EncodedSize];
        BinaryPrimitives.WriteUInt16BigEndian(staged[RecordKindOffset..], (ushort)key.Kind);
        BinaryPrimitives.WriteUInt32BigEndian(staged[DimensionOffset..], key.Dimension.Value);
        WriteSignedOrdered(staged[SectionXOffset..], key.Coordinate.X);
        WriteSignedOrdered(staged[SectionYOffset..], key.Coordinate.Y);
        WriteSignedOrdered(staged[SectionZOffset..], key.Coordinate.Z);
        staged.CopyTo(destination);

        return LogicalEncodeResult.Success();
    }

    /// <summary>Attempts to decode exactly one canonical logical record key.</summary>
    /// <param name="source">An exactly <see cref="EncodedSize"/>-byte canonical source.</param>
    /// <returns>A complete immutable key or a typed validation failure.</returns>
    public static LogicalDecodeResult<LogicalRecordKey> TryDecode(ReadOnlySpan<byte> source)
    {
        if (source.Length != EncodedSize)
        {
            return LogicalDecodeResult<LogicalRecordKey>.Failed(CreateLengthFailure(source.Length));
        }

        LogicalRecordKind kind = (LogicalRecordKind)BinaryPrimitives.ReadUInt16BigEndian(source[RecordKindOffset..]);
        if (!IsSupported(kind))
        {
            return LogicalDecodeResult<LogicalRecordKey>.Failed(LogicalCodecFailure.Create(
                LogicalCodecFailureCode.UnknownRecordKind,
                RecordKindOffset,
                LogicalCodecField.RecordKind));
        }

        DimensionId dimension = new(BinaryPrimitives.ReadUInt32BigEndian(source[DimensionOffset..]));
        SectionCoord coordinate = new(
            ReadSignedOrdered(source[SectionXOffset..]),
            ReadSignedOrdered(source[SectionYOffset..]),
            ReadSignedOrdered(source[SectionZOffset..]));

        return LogicalDecodeResult<LogicalRecordKey>.Success(new LogicalRecordKey(kind, dimension, coordinate));
    }

    private static bool IsSupported(LogicalRecordKind kind)
    {
        return kind == LogicalRecordKind.SectionState;
    }

    private static void WriteSignedOrdered(Span<byte> destination, long value)
    {
        ulong ordered = unchecked((ulong)value) ^ SignedOrderBias;
        BinaryPrimitives.WriteUInt64BigEndian(destination, ordered);
    }

    private static long ReadSignedOrdered(ReadOnlySpan<byte> source)
    {
        ulong bits = BinaryPrimitives.ReadUInt64BigEndian(source) ^ SignedOrderBias;
        return unchecked((long)bits);
    }

    private static LogicalCodecFailure CreateLengthFailure(int actualLength)
    {
        int byteOffset = Math.Min(actualLength, EncodedSize);
        LogicalCodecField field = actualLength switch
        {
            < DimensionOffset => LogicalCodecField.RecordKind,
            < SectionXOffset => LogicalCodecField.Dimension,
            < SectionYOffset => LogicalCodecField.SectionX,
            < SectionZOffset => LogicalCodecField.SectionY,
            < EncodedSize => LogicalCodecField.SectionZ,
            _ => LogicalCodecField.RecordKey,
        };

        return LogicalCodecFailure.Create(LogicalCodecFailureCode.IncorrectLength, byteOffset, field);
    }
}
