namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Identifies one X-contiguous, then Z, then Y position within an explicit section geometry.
/// </summary>
/// <remarks>
/// The CLR representation is an implementation detail and does not select a persistence or wire width.
/// A <see cref="SectionGeometry"/> validates the contextual upper bound before an index is consumed.
/// </remarks>
public readonly record struct LocalIndex : IComparable<LocalIndex>
{
    /// <summary>
    /// Initializes a nonnegative section-local index.
    /// </summary>
    /// <param name="value">The nonnegative scalar value.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is negative.</exception>
    public LocalIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>
    /// Gets the in-memory scalar value. This property does not define a persistence or wire representation.
    /// </summary>
    public int Value { get; }

    /// <inheritdoc />
    public int CompareTo(LocalIndex other)
    {
        return Value.CompareTo(other.Value);
    }

    /// <summary>Compares two local indices by their scalar value.</summary>
    public static bool operator <(LocalIndex left, LocalIndex right)
    {
        return left.Value < right.Value;
    }

    /// <summary>Compares two local indices by their scalar value.</summary>
    public static bool operator <=(LocalIndex left, LocalIndex right)
    {
        return left.Value <= right.Value;
    }

    /// <summary>Compares two local indices by their scalar value.</summary>
    public static bool operator >(LocalIndex left, LocalIndex right)
    {
        return left.Value > right.Value;
    }

    /// <summary>Compares two local indices by their scalar value.</summary>
    public static bool operator >=(LocalIndex left, LocalIndex right)
    {
        return left.Value >= right.Value;
    }

    internal int GetValidatedValue(int sectionVolume)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionVolume);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(Value, sectionVolume, nameof(Value));
        return Value;
    }
}
