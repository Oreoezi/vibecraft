namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Identifies one of the six cardinal directions in the block and section coordinate spaces.
/// </summary>
public enum CoordinateDirection : byte
{
    /// <summary>
    /// The negative X direction.
    /// </summary>
    NegativeX,

    /// <summary>
    /// The positive X direction.
    /// </summary>
    PositiveX,

    /// <summary>
    /// The negative Y direction.
    /// </summary>
    NegativeY,

    /// <summary>
    /// The positive Y direction.
    /// </summary>
    PositiveY,

    /// <summary>
    /// The negative Z direction.
    /// </summary>
    NegativeZ,

    /// <summary>
    /// The positive Z direction.
    /// </summary>
    PositiveZ,
}
