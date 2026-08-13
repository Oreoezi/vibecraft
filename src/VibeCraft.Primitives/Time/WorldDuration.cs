namespace VibeCraft.Primitives.Time;

/// <summary>
/// An exact duration on the fixed world-clock grid, represented as a count of world ticks.
/// </summary>
/// <remarks>
/// The duration is rational seconds (<c>Ticks / 60</c>), not an accumulated rounded
/// nanosecond count. This is distinct from an absolute <see cref="WorldTick"/>.
/// </remarks>
public readonly record struct WorldDuration
{
    /// <summary>
    /// Initializes a duration from an exact count of world ticks.
    /// </summary>
    /// <param name="ticks">The number of fixed 60 TPS world-clock steps.</param>
    public WorldDuration(ulong ticks)
    {
        Ticks = ticks;
    }

    /// <summary>
    /// Gets the number of world ticks in this duration.
    /// </summary>
    public ulong Ticks { get; }

    /// <summary>
    /// Gets the whole rational simulation seconds in this duration.
    /// </summary>
    public ulong WholeSeconds => Ticks / WorldClock.TicksPerSecond;

    /// <summary>
    /// Gets the exact world-tick remainder after <see cref="WholeSeconds"/>.
    /// </summary>
    public uint TicksIntoCurrentSecond => (uint)(Ticks % WorldClock.TicksPerSecond);

    /// <summary>
    /// Gets a duration of zero world ticks.
    /// </summary>
    public static WorldDuration Zero { get; } = new(0);

    /// <summary>
    /// Gets a duration of one world tick.
    /// </summary>
    public static WorldDuration OneTick { get; } = new(1);

    /// <summary>
    /// Attempts to represent this rational duration as an exact <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="timeSpan">The exact duration on success; otherwise <see cref="TimeSpan.Zero"/>.</param>
    /// <returns>The conversion outcome.</returns>
    public WorldDurationTimeSpanResult TryToTimeSpan(out TimeSpan timeSpan)
    {
        if (Ticks % WorldClock.WorldTicksPerExactTimeSpanGroup != 0)
        {
            timeSpan = TimeSpan.Zero;
            return WorldDurationTimeSpanResult.UnrepresentableRemainder;
        }

        ulong exactGroups = Ticks / WorldClock.WorldTicksPerExactTimeSpanGroup;
        if (exactGroups > long.MaxValue / WorldClock.TimeSpanTicksPerThreeWorldTicks)
        {
            timeSpan = TimeSpan.Zero;
            return WorldDurationTimeSpanResult.OutOfRange;
        }

        timeSpan = TimeSpan.FromTicks(checked((long)exactGroups * WorldClock.TimeSpanTicksPerThreeWorldTicks));
        return WorldDurationTimeSpanResult.Exact;
    }

    /// <summary>
    /// Returns this duration as an exact <see cref="TimeSpan"/>.
    /// </summary>
    /// <returns>The exact <see cref="TimeSpan"/>.</returns>
    /// <exception cref="InvalidOperationException">This rational duration is not exactly representable by <see cref="TimeSpan"/>.</exception>
    /// <exception cref="OverflowException">The exact duration exceeds <see cref="TimeSpan"/>'s range.</exception>
    public TimeSpan ToTimeSpanExact()
    {
        WorldDurationTimeSpanResult result = TryToTimeSpan(out TimeSpan timeSpan);
        return result switch
        {
            WorldDurationTimeSpanResult.Exact => timeSpan,
            WorldDurationTimeSpanResult.UnrepresentableRemainder => throw new InvalidOperationException(
                "A 60 TPS world duration is not exactly representable by TimeSpan at this tick count."),
            WorldDurationTimeSpanResult.OutOfRange => throw new OverflowException(
                "The world duration exceeds TimeSpan's range."),
            _ => throw new InvalidOperationException("Unknown world-duration conversion result."),
        };
    }
}

/// <summary>
/// The outcome of converting a <see cref="WorldDuration"/> to <see cref="TimeSpan"/> without rounding.
/// </summary>
public enum WorldDurationTimeSpanResult : byte
{
    /// <summary>
    /// The conversion is exact.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// The exact rational duration has a nonzero fractional <see cref="TimeSpan"/> tick.
    /// </summary>
    UnrepresentableRemainder = 1,

    /// <summary>
    /// The exact duration is outside <see cref="TimeSpan"/>'s range.
    /// </summary>
    OutOfRange = 2,
}
