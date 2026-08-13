namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Defines checked coordinate and local-index operations for one explicit section-side candidate.
/// </summary>
public readonly record struct SectionGeometry
{
    /// <summary>
    /// Initializes geometry for an evaluated section side.
    /// </summary>
    /// <param name="side">The supported side candidate.</param>
    public SectionGeometry(SectionSide side)
    {
        Side = new SectionSide(side.Value);
    }

    /// <summary>
    /// Gets geometry for 16-by-16-by-16 sections.
    /// </summary>
    public static SectionGeometry Side16 { get; } = new(SectionSide.Sixteen);

    /// <summary>
    /// Gets geometry for 32-by-32-by-32 sections.
    /// </summary>
    public static SectionGeometry Side32 { get; } = new(SectionSide.ThirtyTwo);

    /// <summary>
    /// Gets the explicit section-side candidate.
    /// </summary>
    public SectionSide Side { get; }

    /// <summary>
    /// Decomposes a block coordinate using mathematical floor division and modulus on every axis.
    /// </summary>
    /// <param name="block">The block coordinate to decompose.</param>
    /// <returns>The containing section and local block coordinate.</returns>
    public SectionLocation Decompose(BlockCoord block)
    {
        int side = GetSideLength();
        return new SectionLocation(
            new SectionCoord(FloorDivide(block.X, side), FloorDivide(block.Y, side), FloorDivide(block.Z, side)),
            new LocalBlock(FloorMod(block.X, side), FloorMod(block.Y, side), FloorMod(block.Z, side)));
    }

    /// <summary>
    /// Combines a section coordinate and a local block coordinate into an absolute block coordinate.
    /// </summary>
    /// <param name="section">The containing section coordinate.</param>
    /// <param name="local">The section-relative local block coordinate.</param>
    /// <returns>The absolute block coordinate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="local"/> is not valid for this geometry.</exception>
    /// <exception cref="OverflowException">Thrown when the result cannot be represented as a signed 64-bit coordinate.</exception>
    public BlockCoord ToBlockCoord(SectionCoord section, LocalBlock local)
    {
        int side = GetSideLength();
        Validate(local, side);
        return new BlockCoord(
            checked((section.X * side) + local.X),
            checked((section.Y * side) + local.Y),
            checked((section.Z * side) + local.Z));
    }

    /// <summary>
    /// Creates a local block coordinate after validating it against this geometry's side.
    /// </summary>
    /// <param name="x">The local X component.</param>
    /// <param name="y">The local Y component.</param>
    /// <param name="z">The local Z component.</param>
    /// <returns>A local block coordinate valid for this geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a component is outside the side-relative local range.</exception>
    public LocalBlock CreateLocal(int x, int y, int z)
    {
        int side = GetSideLength();
        Validate(x, nameof(x), side);
        Validate(y, nameof(y), side);
        Validate(z, nameof(z), side);
        return new LocalBlock(x, y, z);
    }

    /// <summary>
    /// Returns the inclusive block-coordinate origin of a section.
    /// </summary>
    /// <param name="section">The section coordinate.</param>
    /// <returns>The block coordinate at local position (0, 0, 0).</returns>
    /// <exception cref="OverflowException">Thrown when the origin cannot be represented as a signed 64-bit coordinate.</exception>
    public BlockCoord GetOrigin(SectionCoord section)
    {
        int side = GetSideLength();
        return new BlockCoord(checked(section.X * side), checked(section.Y * side), checked(section.Z * side));
    }

    /// <summary>
    /// Returns the inclusive block-coordinate end of a section.
    /// </summary>
    /// <param name="section">The section coordinate.</param>
    /// <returns>The coordinate at local position (side - 1, side - 1, side - 1).</returns>
    /// <exception cref="OverflowException">Thrown when the inclusive end cannot be represented as a signed 64-bit coordinate.</exception>
    public BlockCoord GetEndInclusive(SectionCoord section)
    {
        int side = GetSideLength();
        BlockCoord origin = GetOrigin(section);
        int finalLocalCoordinate = side - 1;
        return new BlockCoord(
            checked(origin.X + finalLocalCoordinate),
            checked(origin.Y + finalLocalCoordinate),
            checked(origin.Z + finalLocalCoordinate));
    }

    /// <summary>
    /// Returns the X-contiguous, then Z, then Y local index for a valid local coordinate.
    /// </summary>
    /// <param name="local">The section-relative local block coordinate.</param>
    /// <returns>The local index for this geometry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="local"/> is not valid for this geometry.</exception>
    public int GetLinearIndex(LocalBlock local)
    {
        int side = GetSideLength();
        Validate(local, side);
        return checked(local.X + (side * (local.Z + (side * local.Y))));
    }

    private int GetSideLength()
    {
        return Side.GetValidatedValue();
    }

    private static long FloorDivide(long value, int divisor)
    {
        long quotient = value / divisor;
        long remainder = value % divisor;
        return remainder < 0 ? checked(quotient - 1) : quotient;
    }

    private static int FloorMod(long value, int divisor)
    {
        long remainder = value % divisor;
        return checked((int)(remainder < 0 ? remainder + divisor : remainder));
    }

    private static void Validate(LocalBlock local, int side)
    {
        Validate(local.X, nameof(local.X), side);
        Validate(local.Y, nameof(local.Y), side);
        Validate(local.Z, nameof(local.Z), side);
    }

    private static void Validate(int value, string parameterName, int side)
    {
        if (value is < 0 || value >= side)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"A local component must be in the range 0 through {side - 1} for this geometry.");
        }
    }
}
