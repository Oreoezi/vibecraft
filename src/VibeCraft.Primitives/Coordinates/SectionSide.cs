namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Represents one evaluated cubic section-side candidate.
/// </summary>
public readonly record struct SectionSide
{
    /// <summary>
    /// Initializes a supported section side.
    /// </summary>
    /// <param name="value">The side length, which must be 16 or 32 blocks.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not an evaluated candidate.</exception>
    public SectionSide(int value)
    {
        if (value is not (16 or 32))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The only evaluated section-side candidates are 16 and 32.");
        }

        Value = value;
    }

    /// <summary>
    /// Gets the 16-block-side candidate.
    /// </summary>
    public static SectionSide Sixteen { get; } = new(16);

    /// <summary>
    /// Gets the 32-block-side candidate.
    /// </summary>
    public static SectionSide ThirtyTwo { get; } = new(32);

    /// <summary>
    /// Gets the side length in blocks.
    /// </summary>
    public int Value { get; }

    internal int GetValidatedValue()
    {
        return Value switch
        {
            16 or 32 => Value,
            _ => throw new ArgumentOutOfRangeException(nameof(Value), Value, "The only evaluated section-side candidates are 16 and 32."),
        };
    }
}
