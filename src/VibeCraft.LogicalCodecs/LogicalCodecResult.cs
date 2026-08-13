namespace VibeCraft.LogicalCodecs;

/// <summary>Identifies why a bounded logical-codec operation failed.</summary>
public enum LogicalCodecFailureCode : byte
{
    /// <summary>No failure has been selected.</summary>
    Undefined = 0,

    /// <summary>The source or destination length was not the codec's exact required size.</summary>
    IncorrectLength = 1,

    /// <summary>The record-kind value is undefined or unknown to this codec revision.</summary>
    UnknownRecordKind = 2,

    /// <summary>The fixed codec header is absent, malformed, or inconsistent.</summary>
    InvalidHeader = 3,

    /// <summary>The encoded format version is not supported by this codec revision.</summary>
    UnsupportedVersion = 4,

    /// <summary>A bounded count, size, or other codec limit was exceeded.</summary>
    LimitExceeded = 5,

    /// <summary>Checked codec arithmetic overflowed.</summary>
    ArithmeticOverflow = 6,

    /// <summary>An enum value is undefined or unsupported by this codec revision.</summary>
    InvalidEnum = 7,

    /// <summary>Text is malformed, not valid for its field, or not canonically encoded.</summary>
    InvalidText = 8,

    /// <summary>An identity that must be unique occurs more than once.</summary>
    DuplicateIdentity = 9,

    /// <summary>Records or entries do not appear in their required canonical order.</summary>
    NonCanonicalOrder = 10,

    /// <summary>A palette representation is not in its required canonical form.</summary>
    NonCanonicalPalette = 11,

    /// <summary>An index is outside the bounds of the referenced logical collection.</summary>
    IndexOutOfRange = 12,

    /// <summary>A referenced world-state identity cannot be mapped in the active world.</summary>
    UnmappedWorldState = 13,

    /// <summary>Bytes remain after one complete codec value was decoded.</summary>
    TrailingData = 14,

    /// <summary>A scalar or compound value violates its field-specific validity rules.</summary>
    InvalidValue = 15,
}

/// <summary>Identifies the logical field associated with a codec failure.</summary>
public enum LogicalCodecField : byte
{
    /// <summary>No field has been selected.</summary>
    Undefined = 0,

    /// <summary>The complete logical record key.</summary>
    RecordKey = 1,

    /// <summary>The record-kind field.</summary>
    RecordKind = 2,

    /// <summary>The dimension field.</summary>
    Dimension = 3,

    /// <summary>The signed section X field.</summary>
    SectionX = 4,

    /// <summary>The signed section Y field.</summary>
    SectionY = 5,

    /// <summary>The signed section Z field.</summary>
    SectionZ = 6,

    /// <summary>The complete logical projection.</summary>
    Projection = 7,

    /// <summary>The fixed projection header.</summary>
    Header = 8,

    /// <summary>The projection or payload version.</summary>
    Version = 9,

    /// <summary>A world-state mapping or one of its bindings.</summary>
    Mapping = 10,

    /// <summary>A namespaced content key.</summary>
    ContentKey = 11,

    /// <summary>A content property or its value.</summary>
    Property = 12,

    /// <summary>One logical record within a projection.</summary>
    Record = 13,

    /// <summary>The selected side length of a section.</summary>
    Side = 14,

    /// <summary>A section palette or one of its entries.</summary>
    Palette = 15,

    /// <summary>A voxel state or voxel entry.</summary>
    Voxel = 16,

    /// <summary>A sparse collection or one of its entries.</summary>
    Sparse = 17,

    /// <summary>A length-delimited record payload.</summary>
    Payload = 18,

    /// <summary>A scheduled-work collection or entry.</summary>
    Schedule = 19,

    /// <summary>A queue or queue entry.</summary>
    Queue = 20,

    /// <summary>A scheduled item's absolute logical due tick.</summary>
    DueTick = 21,

    /// <summary>A scheduled item's priority.</summary>
    Priority = 22,

    /// <summary>A stable ordering sequence.</summary>
    Sequence = 23,

    /// <summary>A local index within a section.</summary>
    LocalIndex = 24,

    /// <summary>A referenced expected type.</summary>
    ExpectedType = 25,

    /// <summary>A deterministic content or projection digest.</summary>
    Digest = 26,
}

/// <summary>Describes one validated logical-codec failure at an exact byte offset and field.</summary>
public sealed class LogicalCodecFailure
{
    private LogicalCodecFailure(
        LogicalCodecFailureCode code,
        int byteOffset,
        LogicalCodecField field,
        int recordIndex,
        int elementIndex)
    {
        if (!Enum.IsDefined(code) || code == LogicalCodecFailureCode.Undefined)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "A defined logical-codec failure code is required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);

        if (!Enum.IsDefined(field) || field == LogicalCodecField.Undefined)
        {
            throw new ArgumentOutOfRangeException(nameof(field), field, "A defined logical-codec field is required.");
        }

        if (recordIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(recordIndex), recordIndex, "The record index must be nonnegative or -1 when unavailable.");
        }

        if (elementIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex), elementIndex, "The element index must be nonnegative or -1 when unavailable.");
        }

        Code = code;
        ByteOffset = byteOffset;
        Field = field;
        RecordIndex = recordIndex;
        ElementIndex = elementIndex;
    }

    /// <summary>Gets the validated failure category.</summary>
    public LogicalCodecFailureCode Code { get; }

    /// <summary>Gets the nonnegative zero-based byte offset where validation failed.</summary>
    public int ByteOffset { get; }

    /// <summary>Gets the validated logical field associated with the failure.</summary>
    public LogicalCodecField Field { get; }

    /// <summary>Gets the zero-based logical record index, or -1 when no record index applies.</summary>
    public int RecordIndex { get; }

    /// <summary>
    /// Gets the zero-based element index in the field's immediate collection, or -1 when no
    /// element index applies. Top-level collections such as mappings can identify an element
    /// without identifying a record.
    /// </summary>
    public int ElementIndex { get; }

    internal static LogicalCodecFailure Create(
        LogicalCodecFailureCode code,
        int byteOffset,
        LogicalCodecField field)
    {
        return new LogicalCodecFailure(code, byteOffset, field, -1, -1);
    }

    /// <summary>
    /// Creates a validated logical-codec failure with optional record and element positions.
    /// </summary>
    /// <param name="code">A defined, nonzero failure code.</param>
    /// <param name="byteOffset">The nonnegative zero-based byte offset.</param>
    /// <param name="field">A defined, nonzero logical field.</param>
    /// <param name="recordIndex">A nonnegative record index, or -1 when no record applies.</param>
    /// <param name="elementIndex">A nonnegative element index, or -1 when no element applies.</param>
    /// <returns>An immutable validated failure.</returns>
    internal static LogicalCodecFailure Create(
        LogicalCodecFailureCode code,
        int byteOffset,
        LogicalCodecField field,
        int recordIndex,
        int elementIndex)
    {
        return new LogicalCodecFailure(code, byteOffset, field, recordIndex, elementIndex);
    }
}

/// <summary>Reports success or a typed failure for a logical encode operation.</summary>
public sealed class LogicalEncodeResult
{
    private LogicalEncodeResult()
    {
    }

    private LogicalEncodeResult(LogicalCodecFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets whether encoding completed and published the entire staged value.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Gets the typed failure, or <see langword="null"/> on success.</summary>
    public LogicalCodecFailure? Failure { get; }

    internal static LogicalEncodeResult Success()
    {
        return new LogicalEncodeResult();
    }

    internal static LogicalEncodeResult Failed(LogicalCodecFailure failure)
    {
        return new LogicalEncodeResult(failure);
    }
}

/// <summary>Reports an immutable decoded value or a typed failure.</summary>
/// <typeparam name="T">The decoded logical value type.</typeparam>
public sealed class LogicalDecodeResult<T>
    where T : notnull
{
    private T? DecodedValue { get; }

    private LogicalDecodeResult(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        DecodedValue = value;
    }

    private LogicalDecodeResult(LogicalCodecFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets whether decoding produced one complete validated value.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Gets the non-null decoded value.</summary>
    /// <exception cref="InvalidOperationException">The result represents a failure and has no value.</exception>
    public T Value => Succeeded
        ? DecodedValue!
        : throw new InvalidOperationException("A failed logical decode result has no value.");

    /// <summary>Gets the typed failure, or <see langword="null"/> on success.</summary>
    public LogicalCodecFailure? Failure { get; }

    internal static LogicalDecodeResult<T> Success(T value)
    {
        return new LogicalDecodeResult<T>(value);
    }

    internal static LogicalDecodeResult<T> Failed(LogicalCodecFailure failure)
    {
        return new LogicalDecodeResult<T>(failure);
    }
}
