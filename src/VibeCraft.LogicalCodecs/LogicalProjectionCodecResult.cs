using System.Collections.Immutable;

namespace VibeCraft.LogicalCodecs;

/// <summary>Contains one owned canonical logical-projection fixture encoding and its digest.</summary>
/// <remarks>
/// The encoding is a storage-neutral G1 fixture value, not a persistence, database, migration,
/// wire, or user-world format.
/// </remarks>
public sealed class LogicalProjectionEncoding
{
    internal LogicalProjectionEncoding(ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Encoded fixture bytes must be initialized and nonempty.", nameof(bytes));
        }

        Bytes = bytes;
        Digest = LogicalProjectionDigest.Compute(bytes.AsSpan());
    }

    /// <summary>Gets the owned immutable complete fixture encoding.</summary>
    public ImmutableArray<byte> Bytes { get; }

    /// <summary>Gets the SHA-256 digest of <see cref="Bytes"/>.</summary>
    public LogicalProjectionDigest Digest { get; }
}

/// <summary>Reports one owned logical-projection fixture encoding or a typed canonical-codec failure.</summary>
public sealed class LogicalProjectionEncodeResult
{
    private LogicalProjectionEncodeResult(LogicalProjectionEncoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        EncodedValue = value;
    }

    private LogicalProjectionEncodeResult(LogicalCodecFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    private LogicalProjectionEncoding? EncodedValue { get; }

    /// <summary>Gets whether the complete staged fixture encoding was produced.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Gets the owned complete encoding on success.</summary>
    /// <exception cref="InvalidOperationException">Thrown when this result represents a failure.</exception>
    public LogicalProjectionEncoding Value => Succeeded
        ? EncodedValue!
        : throw new InvalidOperationException("A failed logical projection encode result has no value.");

    /// <summary>Gets the typed failure, or <see langword="null"/> on success.</summary>
    public LogicalCodecFailure? Failure { get; }

    internal static LogicalProjectionEncodeResult Success(LogicalProjectionEncoding value)
    {
        return new LogicalProjectionEncodeResult(value);
    }

    internal static LogicalProjectionEncodeResult Failed(LogicalCodecFailure failure)
    {
        return new LogicalProjectionEncodeResult(failure);
    }
}
