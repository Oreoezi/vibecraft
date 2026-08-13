namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Identifies a world dimension independently of its coordinate space.
/// </summary>
public readonly record struct DimensionId
{
    /// <summary>
    /// Initializes a dimension identity.
    /// </summary>
    /// <param name="value">The unsigned 32-bit dimension identity.</param>
    public DimensionId(uint value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the unsigned 32-bit dimension identity.
    /// </summary>
    public uint Value { get; }
}
