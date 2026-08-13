namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Represents a signed section coordinate in logical X, Y, Z order.
/// </summary>
public readonly record struct SectionCoord
{
    /// <summary>
    /// Initializes a section coordinate.
    /// </summary>
    /// <param name="x">The signed section X component.</param>
    /// <param name="y">The signed section Y component.</param>
    /// <param name="z">The signed section Z component.</param>
    public SectionCoord(long x, long y, long z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Gets the signed section X component.
    /// </summary>
    public long X { get; }

    /// <summary>
    /// Gets the signed section Y component.
    /// </summary>
    public long Y { get; }

    /// <summary>
    /// Gets the signed section Z component.
    /// </summary>
    public long Z { get; }

    /// <summary>
    /// Gets the column coordinate that contains this section.
    /// </summary>
    public ColumnCoord Column => new(X, Z);

    /// <summary>
    /// Returns this coordinate translated by the supplied signed section deltas.
    /// </summary>
    /// <param name="deltaX">The X delta in sections.</param>
    /// <param name="deltaY">The Y delta in sections.</param>
    /// <param name="deltaZ">The Z delta in sections.</param>
    /// <returns>The translated section coordinate.</returns>
    /// <exception cref="OverflowException">Thrown when a component cannot be represented as a signed 64-bit integer.</exception>
    public SectionCoord Offset(long deltaX, long deltaY, long deltaZ)
    {
        return new SectionCoord(checked(X + deltaX), checked(Y + deltaY), checked(Z + deltaZ));
    }

    /// <summary>
    /// Returns the adjacent section in a cardinal direction.
    /// </summary>
    /// <param name="direction">The direction of the neighbor.</param>
    /// <returns>The adjacent section coordinate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a cardinal direction.</exception>
    /// <exception cref="OverflowException">Thrown when the neighbor cannot be represented as a signed 64-bit integer.</exception>
    public SectionCoord Neighbor(CoordinateDirection direction)
    {
        return direction switch
        {
            CoordinateDirection.NegativeX => Offset(-1, 0, 0),
            CoordinateDirection.PositiveX => Offset(1, 0, 0),
            CoordinateDirection.NegativeY => Offset(0, -1, 0),
            CoordinateDirection.PositiveY => Offset(0, 1, 0),
            CoordinateDirection.NegativeZ => Offset(0, 0, -1),
            CoordinateDirection.PositiveZ => Offset(0, 0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "A cardinal coordinate direction is required."),
        };
    }

    /// <summary>
    /// Returns the inclusive block-coordinate origin of this section for a geometry.
    /// </summary>
    /// <param name="geometry">The section geometry.</param>
    /// <returns>The block coordinate at local position (0, 0, 0).</returns>
    /// <exception cref="OverflowException">Thrown when the origin cannot be represented as a signed 64-bit coordinate.</exception>
    public BlockCoord Origin(SectionGeometry geometry)
    {
        return geometry.GetOrigin(this);
    }

    /// <summary>
    /// Returns the inclusive block-coordinate end of this section for a geometry.
    /// </summary>
    /// <param name="geometry">The section geometry.</param>
    /// <returns>The coordinate at local position (side - 1, side - 1, side - 1).</returns>
    /// <exception cref="OverflowException">Thrown when the inclusive end cannot be represented as a signed 64-bit coordinate.</exception>
    public BlockCoord EndInclusive(SectionGeometry geometry)
    {
        return geometry.GetEndInclusive(this);
    }
}
