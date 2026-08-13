using System.Collections.Immutable;
using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Defines one immutable sparse semantic payload associated with a section local index.
/// </summary>
/// <remarks>
/// This is a storage-neutral fixture value, not a persistence, database, migration, wire, or
/// user-world record. Its payload has no codec or byte-layout meaning in this model.
/// </remarks>
public sealed class LogicalSparseRecord
{
    /// <summary>The largest payload admitted for one sparse semantic fixture record.</summary>
    public const int MaxPayloadBytes = 64 * 1024;

    private LogicalSparseRecord(LocalIndex localIndex, NamespacedContentId type, ImmutableArray<byte> payload)
    {
        LocalIndex = localIndex;
        Type = type;
        Payload = payload;
    }

    /// <summary>Gets the X-contiguous/Z/Y local index for this sparse record.</summary>
    public LocalIndex LocalIndex { get; }

    /// <summary>Gets the canonical content type that owns this sparse payload.</summary>
    public NamespacedContentId Type { get; }

    /// <summary>Gets the copied opaque semantic payload.</summary>
    public ImmutableArray<byte> Payload { get; }

    internal static ImmutableArray<LogicalSparseRecord> CreateCanonical(
        ImmutableArray<LogicalSparseInput> inputs,
        int volume)
    {
        HashSet<LocalIndex> localIndices = [];
        List<LogicalSparseRecord> records = [];
        foreach (LogicalSparseInput input in inputs)
        {
            input.ThrowIfInvalid();
            ValidateLocalIndex(input.LocalIndex, volume, nameof(inputs));
            if (!localIndices.Add(input.LocalIndex))
            {
                throw new ArgumentException(
                    $"A sparse record for local index {input.LocalIndex} is supplied more than once.",
                    nameof(inputs));
            }

            records.Add(new LogicalSparseRecord(input.LocalIndex, input.Type, input.Payload));
        }

        return [.. records.OrderBy(record => record.LocalIndex)];
    }

    private static void ValidateLocalIndex(LocalIndex localIndex, int volume, string parameterName)
    {
        if (localIndex.Value >= volume)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                localIndex.Value,
                $"A sparse local index must be in the range 0 through {volume - 1}.");
        }
    }
}

/// <summary>
/// Defines copied caller input for one sparse semantic payload.
/// </summary>
/// <remarks>
/// This is storage-neutral fixture input, not a persistence, database, migration, wire, or
/// user-world format.
/// </remarks>
public readonly record struct LogicalSparseInput
{
    /// <summary>
    /// Initializes sparse semantic input and deep-copies its payload.
    /// </summary>
    /// <param name="localIndex">The section-local index; geometry validates its contextual upper bound.</param>
    /// <param name="type">The canonical content type that owns the payload.</param>
    /// <param name="payload">The opaque payload to copy.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the payload is too large.</exception>
    public LogicalSparseInput(LocalIndex localIndex, NamespacedContentId type, ReadOnlyMemory<byte> payload)
    {
        if (!type.IsValid)
        {
            throw new ArgumentException("A validated canonical sparse namespaced content ID is required.", nameof(type));
        }

        if (payload.Length > LogicalSparseRecord.MaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                $"A sparse payload may contain at most {LogicalSparseRecord.MaxPayloadBytes} bytes.");
        }

        LocalIndex = localIndex;
        Type = type;
        Payload = [.. payload.Span];
    }

    private LogicalSparseInput(LocalIndex localIndex, NamespacedContentId type, ImmutableArray<byte> payload)
    {
        LocalIndex = localIndex;
        Type = type;
        Payload = payload;
    }

    /// <summary>Gets the local index supplied by the caller.</summary>
    public LocalIndex LocalIndex { get; }

    /// <summary>Gets the canonical content type supplied by the caller.</summary>
    public NamespacedContentId Type { get; }

    /// <summary>Gets the deep-copied opaque sparse payload.</summary>
    public ImmutableArray<byte> Payload { get; }

    internal static LogicalSparseInput FromEncoded(LocalIndex localIndex, NamespacedContentId type, ReadOnlySpan<byte> payload)
    {
        NamespacedContentId validatedType = type.IsValid
            ? type
            : throw new ArgumentException("A validated canonical sparse namespaced content ID is required.", nameof(type));

        return payload.Length <= LogicalSparseRecord.MaxPayloadBytes
            ? new LogicalSparseInput(localIndex, validatedType, [.. payload])
            : throw new ArgumentOutOfRangeException(
                nameof(payload),
                $"A sparse payload may contain at most {LogicalSparseRecord.MaxPayloadBytes} bytes.");
    }

    internal void ThrowIfInvalid()
    {
        if (!Type.IsValid || Payload.IsDefault || Payload.Length > LogicalSparseRecord.MaxPayloadBytes)
        {
            throw new InvalidOperationException("LogicalSparseInput is uninitialized or invalid.");
        }
    }
}
