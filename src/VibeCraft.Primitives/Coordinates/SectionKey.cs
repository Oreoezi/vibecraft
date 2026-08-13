namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Identifies a section by its dimension and its signed section coordinate.
/// </summary>
public readonly record struct SectionKey
{
    /// <summary>
    /// Initializes a section key.
    /// </summary>
    /// <param name="dimension">The owning dimension identity.</param>
    /// <param name="coordinate">The signed section coordinate.</param>
    public SectionKey(DimensionId dimension, SectionCoord coordinate)
    {
        Dimension = dimension;
        Coordinate = coordinate;
    }

    /// <summary>
    /// Gets the owning dimension identity.
    /// </summary>
    public DimensionId Dimension { get; }

    /// <summary>
    /// Gets the signed section coordinate.
    /// </summary>
    public SectionCoord Coordinate { get; }
}
