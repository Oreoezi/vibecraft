namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Represents a non-empty vertical block range with an inclusive minimum and exclusive maximum.
/// </summary>
public readonly record struct BlockYRange
{
    /// <summary>
    /// Initializes a vertical block range.
    /// </summary>
    /// <param name="minY">The inclusive minimum block Y value.</param>
    /// <param name="maxYExclusive">The exclusive maximum block Y value.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum is not greater than the minimum.</exception>
    public BlockYRange(long minY, long maxYExclusive)
    {
        if (maxYExclusive <= minY)
        {
            throw new ArgumentOutOfRangeException(nameof(maxYExclusive), maxYExclusive, "The exclusive maximum Y value must be greater than the inclusive minimum Y value.");
        }

        MinY = minY;
        MaxYExclusive = maxYExclusive;
    }

    /// <summary>
    /// Gets the inclusive minimum block Y value.
    /// </summary>
    public long MinY { get; }

    /// <summary>
    /// Gets the exclusive maximum block Y value.
    /// </summary>
    public long MaxYExclusive { get; }

    /// <summary>
    /// Gets a value indicating whether this instance represents a non-empty range.
    /// </summary>
    public bool IsValid => MaxYExclusive > MinY;

    /// <summary>
    /// Determines whether a block Y value is in this range.
    /// </summary>
    /// <param name="y">The block Y value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="y"/> is in this range; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this range is invalid or default-initialized.</exception>
    public bool Contains(long y)
    {
        ThrowIfInvalid();
        return y >= MinY && y < MaxYExclusive;
    }

    /// <summary>
    /// Determines whether another range is fully contained by this range.
    /// </summary>
    /// <param name="other">The range to test.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> is fully contained by this range; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this range is invalid or default-initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="other"/> is invalid or default-initialized.</exception>
    public bool Contains(BlockYRange other)
    {
        ThrowIfInvalid();
        return other.IsValid
            ? other.MinY >= MinY && other.MaxYExclusive <= MaxYExclusive
            : throw new ArgumentOutOfRangeException(nameof(other), other, "A valid, non-empty block Y range is required.");
    }

    private void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("The block Y range is invalid or default-initialized.");
        }
    }
}
