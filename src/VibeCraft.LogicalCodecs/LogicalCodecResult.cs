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
}

/// <summary>Describes one validated logical-codec failure at an exact byte offset and field.</summary>
public sealed class LogicalCodecFailure
{
    private LogicalCodecFailure(
        LogicalCodecFailureCode code,
        int byteOffset,
        LogicalCodecField field)
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

        Code = code;
        ByteOffset = byteOffset;
        Field = field;
    }

    /// <summary>Gets the validated failure category.</summary>
    public LogicalCodecFailureCode Code { get; }

    /// <summary>Gets the nonnegative zero-based byte offset where validation failed.</summary>
    public int ByteOffset { get; }

    /// <summary>Gets the validated logical field associated with the failure.</summary>
    public LogicalCodecField Field { get; }

    internal static LogicalCodecFailure Create(
        LogicalCodecFailureCode code,
        int byteOffset,
        LogicalCodecField field)
    {
        return new LogicalCodecFailure(code, byteOffset, field);
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
