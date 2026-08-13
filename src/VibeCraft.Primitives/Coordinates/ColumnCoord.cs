namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Represents the X and Z coordinates of a vertical section column.
/// </summary>
public readonly record struct ColumnCoord
{
    /// <summary>
    /// Initializes a column coordinate.
    /// </summary>
    /// <param name="x">The signed section X component.</param>
    /// <param name="z">The signed section Z component.</param>
    public ColumnCoord(long x, long z)
    {
        X = x;
        Z = z;
    }

    /// <summary>
    /// Gets the signed section X component.
    /// </summary>
    public long X { get; }

    /// <summary>
    /// Gets the signed section Z component.
    /// </summary>
    public long Z { get; }

    /// <summary>
    /// Returns this column translated by the supplied signed section deltas.
    /// </summary>
    /// <param name="deltaX">The X delta in sections.</param>
    /// <param name="deltaZ">The Z delta in sections.</param>
    /// <returns>The translated column coordinate.</returns>
    /// <exception cref="OverflowException">Thrown when a component cannot be represented as a signed 64-bit integer.</exception>
    public ColumnCoord Offset(long deltaX, long deltaZ)
    {
        return new ColumnCoord(checked(X + deltaX), checked(Z + deltaZ));
    }
}
