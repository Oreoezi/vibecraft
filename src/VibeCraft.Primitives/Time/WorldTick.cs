namespace VibeCraft.Primitives.Time;

/// <summary>
/// Server-owned absolute logical time on the fixed 60 TPS world clock.
/// </summary>
/// <remarks>
/// A world tick is not a client sequence, prediction step, or elapsed duration. It never wraps:
/// reaching <see cref="ulong.MaxValue"/> exhausts the world clock.
/// </remarks>
public readonly record struct WorldTick : IComparable<WorldTick>
{
    /// <summary>
    /// Initializes an authoritative world tick.
    /// </summary>
    /// <param name="value">The absolute unsigned 64-bit world-clock value.</param>
    public WorldTick(ulong value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the absolute world-clock value.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    /// Gets the first world tick.
    /// </summary>
    public static WorldTick Initial { get; } = new(0);

    /// <summary>
    /// Compares this absolute world tick with another in unsigned numeric order.
    /// </summary>
    /// <param name="other">The world tick to compare against.</param>
    /// <returns>A value less than zero, zero, or greater than zero when this tick precedes, equals, or follows <paramref name="other"/>.</returns>
    public int CompareTo(WorldTick other)
    {
        return Value.CompareTo(other.Value);
    }

    /// <summary>
    /// Attempts to advance this tick by one without wrapping.
    /// </summary>
    /// <param name="next">The next tick on success; otherwise this tick.</param>
    /// <returns>Whether the world clock advanced or is exhausted.</returns>
    public WorldTickAdvanceResult TryNext(out WorldTick next)
    {
        return TryAdvance(WorldDuration.OneTick, out next);
    }

    /// <summary>
    /// Attempts to advance this tick by an exact world-clock duration without wrapping.
    /// </summary>
    /// <param name="duration">The number of fixed world-clock steps to advance.</param>
    /// <param name="advanced">The advanced tick on success; otherwise this tick.</param>
    /// <returns>Whether the world clock advanced or is exhausted.</returns>
    public WorldTickAdvanceResult TryAdvance(WorldDuration duration, out WorldTick advanced)
    {
        if (duration.Ticks > ulong.MaxValue - Value)
        {
            advanced = this;
            return WorldTickAdvanceResult.Exhausted;
        }

        advanced = new WorldTick(checked(Value + duration.Ticks));
        return WorldTickAdvanceResult.Advanced;
    }

    /// <summary>
    /// Determines whether one world tick precedes another.
    /// </summary>
    /// <param name="left">The left world tick.</param>
    /// <param name="right">The right world tick.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> precedes <paramref name="right"/>.</returns>
    public static bool operator <(WorldTick left, WorldTick right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines whether one world tick precedes or equals another.
    /// </summary>
    /// <param name="left">The left world tick.</param>
    /// <param name="right">The right world tick.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> precedes or equals <paramref name="right"/>.</returns>
    public static bool operator <=(WorldTick left, WorldTick right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Determines whether one world tick follows another.
    /// </summary>
    /// <param name="left">The left world tick.</param>
    /// <param name="right">The right world tick.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> follows <paramref name="right"/>.</returns>
    public static bool operator >(WorldTick left, WorldTick right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Determines whether one world tick follows or equals another.
    /// </summary>
    /// <param name="left">The left world tick.</param>
    /// <param name="right">The right world tick.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> follows or equals <paramref name="right"/>.</returns>
    public static bool operator >=(WorldTick left, WorldTick right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// The result of trying to advance a <see cref="WorldTick"/>.
/// </summary>
public enum WorldTickAdvanceResult : byte
{
    /// <summary>
    /// The world clock advanced by the requested duration.
    /// </summary>
    Advanced = 0,

    /// <summary>
    /// The world clock would exceed <see cref="ulong.MaxValue"/>; no wrap is permitted.
    /// </summary>
    Exhausted = 1,
}
