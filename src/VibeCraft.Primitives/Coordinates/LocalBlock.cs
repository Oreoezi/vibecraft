namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Represents an unsigned local block coordinate whose section-relative bounds are validated by a <see cref="SectionGeometry"/>.
/// </summary>
public readonly record struct LocalBlock
{
    /// <summary>
    /// Initializes a local block coordinate with byte-sized unsigned components.
    /// </summary>
    /// <param name="x">The local X component.</param>
    /// <param name="y">The local Y component.</param>
    /// <param name="z">The local Z component.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a component is outside the byte domain.</exception>
    public LocalBlock(int x, int y, int z)
    {
        X = ToByte(x, nameof(x));
        Y = ToByte(y, nameof(y));
        Z = ToByte(z, nameof(z));
    }

    /// <summary>
    /// Gets the unsigned local X component.
    /// </summary>
    public byte X { get; }

    /// <summary>
    /// Gets the unsigned local Y component.
    /// </summary>
    public byte Y { get; }

    /// <summary>
    /// Gets the unsigned local Z component.
    /// </summary>
    public byte Z { get; }

    private static byte ToByte(int value, string parameterName)
    {
        return value is >= byte.MinValue and <= byte.MaxValue
            ? (byte)value
            : throw new ArgumentOutOfRangeException(parameterName, value, "A local component must be in the byte domain 0 through 255.");
    }
}
