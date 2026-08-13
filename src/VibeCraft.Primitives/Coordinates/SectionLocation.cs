namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Couples a section coordinate with a validated local block coordinate.
/// </summary>
public readonly record struct SectionLocation
{
    /// <summary>
    /// Initializes a decomposed block location.
    /// </summary>
    /// <param name="section">The containing section coordinate.</param>
    /// <param name="local">The section-relative local block coordinate.</param>
    public SectionLocation(SectionCoord section, LocalBlock local)
    {
        Section = section;
        Local = local;
    }

    /// <summary>
    /// Gets the containing section coordinate.
    /// </summary>
    public SectionCoord Section { get; }

    /// <summary>
    /// Gets the section-relative local block coordinate.
    /// </summary>
    public LocalBlock Local { get; }
}
