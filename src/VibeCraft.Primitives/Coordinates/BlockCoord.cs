namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Represents a signed, integer block coordinate in logical X, Y, Z order.
/// </summary>
public readonly record struct BlockCoord
{
    /// <summary>
    /// Initializes a block coordinate.
    /// </summary>
    /// <param name="x">The signed X component.</param>
    /// <param name="y">The signed Y component.</param>
    /// <param name="z">The signed Z component.</param>
    public BlockCoord(long x, long y, long z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Gets the signed X component.
    /// </summary>
    public long X { get; }

    /// <summary>
    /// Gets the signed Y component.
    /// </summary>
    public long Y { get; }

    /// <summary>
    /// Gets the signed Z component.
    /// </summary>
    public long Z { get; }

    /// <summary>
    /// Returns this coordinate translated by the supplied signed deltas.
    /// </summary>
    /// <param name="deltaX">The X delta.</param>
    /// <param name="deltaY">The Y delta.</param>
    /// <param name="deltaZ">The Z delta.</param>
    /// <returns>The translated block coordinate.</returns>
    /// <exception cref="OverflowException">Thrown when a component cannot be represented as a signed 64-bit integer.</exception>
    public BlockCoord Offset(long deltaX, long deltaY, long deltaZ)
    {
        return new BlockCoord(checked(X + deltaX), checked(Y + deltaY), checked(Z + deltaZ));
    }

    /// <summary>
    /// Returns the adjacent coordinate in a cardinal direction.
    /// </summary>
    /// <param name="direction">The direction of the neighbor.</param>
    /// <returns>The adjacent block coordinate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a cardinal direction.</exception>
    /// <exception cref="OverflowException">Thrown when the neighbor cannot be represented as a signed 64-bit integer.</exception>
    public BlockCoord Neighbor(CoordinateDirection direction)
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
}
