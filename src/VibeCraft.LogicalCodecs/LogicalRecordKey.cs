using VibeCraft.Primitives.Coordinates;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Defines the typed logical tuple <c>(record kind, dimension, X, Y, Z)</c> used by G1 fixtures.
/// </summary>
/// <remarks>
/// This is storage-neutral logical identity. It is not a database key, persistence format,
/// user-world format, or wire-protocol address.
/// </remarks>
public readonly record struct LogicalRecordKey
{
    /// <summary>Initializes a logical key from its record family and section identity.</summary>
    /// <param name="kind">The logical record family.</param>
    /// <param name="section">The dimension and signed section coordinate.</param>
    public LogicalRecordKey(LogicalRecordKind kind, SectionKey section)
    {
        Kind = kind;
        Section = section;
    }

    /// <summary>Initializes a logical key from its complete typed tuple.</summary>
    /// <param name="kind">The logical record family.</param>
    /// <param name="dimension">The dimension identity.</param>
    /// <param name="coordinate">The signed section coordinate.</param>
    public LogicalRecordKey(LogicalRecordKind kind, DimensionId dimension, SectionCoord coordinate)
        : this(kind, new SectionKey(dimension, coordinate))
    {
    }

    /// <summary>Gets the logical record family.</summary>
    public LogicalRecordKind Kind { get; }

    /// <summary>Gets the dimension and signed section coordinate.</summary>
    public SectionKey Section { get; }

    /// <summary>Gets the dimension identity.</summary>
    public DimensionId Dimension => Section.Dimension;

    /// <summary>Gets the signed section coordinate in X, Y, Z component order.</summary>
    public SectionCoord Coordinate => Section.Coordinate;

}
