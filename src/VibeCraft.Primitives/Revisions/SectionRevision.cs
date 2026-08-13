namespace VibeCraft.Primitives.Revisions;

/// <summary>
/// Monotonically increasing revision owned by one section.
/// </summary>
/// <remarks>
/// This is an in-memory scalar domain, not a persistence or wire representation.
/// </remarks>
public readonly record struct SectionRevision : IComparable<SectionRevision>
{
    /// <summary>
    /// Initializes a section revision.
    /// </summary>
    /// <param name="value">A nonnegative signed 64-bit revision value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public SectionRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>
    /// Gets the underlying nonnegative revision value.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Gets the initial revision for a section.
    /// </summary>
    public static SectionRevision Initial { get; } = new(0);

    /// <summary>
    /// Compares this revision with another revision in signed numeric order.
    /// </summary>
    /// <param name="other">The revision to compare against.</param>
    /// <returns>A value less than zero, zero, or greater than zero when this revision precedes, equals, or follows <paramref name="other"/>.</returns>
    public int CompareTo(SectionRevision other)
    {
        return Value.CompareTo(other.Value);
    }

    /// <summary>
    /// Attempts to advance this revision by one without wrapping.
    /// </summary>
    /// <param name="next">The advanced revision on success; otherwise this revision.</param>
    /// <returns>Whether the revision advanced or is exhausted.</returns>
    public SectionRevisionAdvanceResult TryNext(out SectionRevision next)
    {
        if (Value == long.MaxValue)
        {
            next = this;
            return SectionRevisionAdvanceResult.Exhausted;
        }

        next = new SectionRevision(checked(Value + 1));
        return SectionRevisionAdvanceResult.Advanced;
    }

    /// <summary>
    /// Determines whether one section revision precedes another.
    /// </summary>
    /// <param name="left">The left revision.</param>
    /// <param name="right">The right revision.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> precedes <paramref name="right"/>.</returns>
    public static bool operator <(SectionRevision left, SectionRevision right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines whether one section revision precedes or equals another.
    /// </summary>
    /// <param name="left">The left revision.</param>
    /// <param name="right">The right revision.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> precedes or equals <paramref name="right"/>.</returns>
    public static bool operator <=(SectionRevision left, SectionRevision right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Determines whether one section revision follows another.
    /// </summary>
    /// <param name="left">The left revision.</param>
    /// <param name="right">The right revision.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> follows <paramref name="right"/>.</returns>
    public static bool operator >(SectionRevision left, SectionRevision right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Determines whether one section revision follows or equals another.
    /// </summary>
    /// <param name="left">The left revision.</param>
    /// <param name="right">The right revision.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> follows or equals <paramref name="right"/>.</returns>
    public static bool operator >=(SectionRevision left, SectionRevision right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// The result of trying to advance a <see cref="SectionRevision"/>.
/// </summary>
public enum SectionRevisionAdvanceResult : byte
{
    /// <summary>
    /// The revision advanced by one.
    /// </summary>
    Advanced = 0,

    /// <summary>
    /// The maximum signed 64-bit revision has been reached; no wrap is permitted.
    /// </summary>
    Exhausted = 1,
}
