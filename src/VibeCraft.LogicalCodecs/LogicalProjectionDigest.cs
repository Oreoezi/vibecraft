using System.Collections.Immutable;
using System.Security.Cryptography;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Defines the immutable SHA-256 digest of one complete canonical logical-projection fixture encoding.
/// </summary>
/// <remarks>
/// This value identifies G1 fixture bytes only. It is not a user-world checksum, persistence checksum,
/// database value, migration artifact, or wire-protocol checksum.
/// </remarks>
public sealed class LogicalProjectionDigest : IEquatable<LogicalProjectionDigest>
{
    /// <summary>The exact SHA-256 digest length in bytes.</summary>
    public const int ByteLength = 32;

    private ImmutableArray<byte> BytesInternal { get; }

    private LogicalProjectionDigest(ImmutableArray<byte> bytes)
    {
        BytesInternal = bytes;
    }

    /// <summary>Gets an immutable copy of the digest bytes.</summary>
    public ImmutableArray<byte> Bytes => BytesInternal;

    /// <summary>Creates the SHA-256 digest for one complete encoded fixture value.</summary>
    /// <param name="encodedBytes">The complete canonical fixture encoding.</param>
    /// <returns>A distinct immutable digest value.</returns>
    public static LogicalProjectionDigest Compute(ReadOnlySpan<byte> encodedBytes)
    {
        return new LogicalProjectionDigest([.. SHA256.HashData(encodedBytes)]);
    }

    /// <summary>Parses exactly one lowercase hexadecimal SHA-256 digest.</summary>
    /// <param name="hex">The 64-character lowercase hexadecimal digest.</param>
    /// <returns>The parsed digest.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a canonical lowercase SHA-256 digest.</exception>
    public static LogicalProjectionDigest Parse(string hex)
    {
        return TryParse(hex, out LogicalProjectionDigest? digest)
            ? digest!
            : throw new FormatException("A logical-projection digest must be 64 lowercase hexadecimal characters.");
    }

    /// <summary>Attempts to parse exactly one lowercase hexadecimal SHA-256 digest.</summary>
    /// <param name="hex">The candidate 64-character lowercase hexadecimal digest.</param>
    /// <param name="digest">The parsed digest on success; otherwise <see langword="null"/>.</param>
    /// <returns>Whether the text was a canonical lowercase SHA-256 digest.</returns>
    public static bool TryParse(string? hex, out LogicalProjectionDigest? digest)
    {
        digest = null;
        if (hex is null || hex.Length != ByteLength * 2 || hex.Any(character => character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f'))))
        {
            return false;
        }

        try
        {
            byte[] parsed = Convert.FromHexString(hex);
            digest = new LogicalProjectionDigest([.. parsed]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool Equals(LogicalProjectionDigest? other)
    {
        return other is not null && BytesInternal.AsSpan().SequenceEqual(other.BytesInternal.AsSpan());
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as LogicalProjectionDigest);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (byte value in BytesInternal)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <summary>Renders this digest as canonical lowercase hexadecimal text.</summary>
    /// <returns>The 64-character lowercase hexadecimal digest.</returns>
    public override string ToString()
    {
        return Convert.ToHexStringLower(BytesInternal.AsSpan());
    }
}
