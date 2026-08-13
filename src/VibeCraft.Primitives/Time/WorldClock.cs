namespace VibeCraft.Primitives.Time;

/// <summary>
/// Exact conversion rules for VibeCraft's single fixed 60 TPS authoritative world clock.
/// </summary>
public static class WorldClock
{
    /// <summary>
    /// The fixed number of authoritative world ticks in one rational simulation second.
    /// </summary>
    public const uint TicksPerSecond = 60;

    internal const ulong WorldTicksPerExactTimeSpanGroup = 3;
    internal const long TimeSpanTicksPerThreeWorldTicks = TimeSpan.TicksPerSecond / 20;

    /// <summary>
    /// Converts a nonnegative <see cref="TimeSpan"/> to a world duration using a named rounding rule.
    /// </summary>
    /// <param name="timeSpan">The source duration.</param>
    /// <param name="rounding">The required loss policy when the source is not exactly on the 60 TPS grid.</param>
    /// <param name="duration">The converted duration on success; otherwise <see cref="WorldDuration.Zero"/>.</param>
    /// <returns>The exact, rounded, or failed conversion outcome.</returns>
    public static TimeSpanWorldDurationResult TryFromTimeSpan(
        TimeSpan timeSpan,
        WorldDurationRounding rounding,
        out WorldDuration duration)
    {
        if (rounding is not WorldDurationRounding.RequireExact
            and not WorldDurationRounding.Down
            and not WorldDurationRounding.Up)
        {
            duration = WorldDuration.Zero;
            return TimeSpanWorldDurationResult.InvalidRounding;
        }

        if (timeSpan.Ticks < 0)
        {
            duration = WorldDuration.Zero;
            return TimeSpanWorldDurationResult.NegativeInput;
        }

        ulong sourceTicks = (ulong)timeSpan.Ticks;
        ulong wholeSeconds = sourceTicks / TimeSpan.TicksPerSecond;
        ulong subsecondTicks = sourceTicks % TimeSpan.TicksPerSecond;
        ulong wholeWorldTicks = checked(wholeSeconds * TicksPerSecond);
        ulong scaledSubsecondTicks = checked(subsecondTicks * TicksPerSecond);
        ulong additionalWorldTicks = scaledSubsecondTicks / TimeSpan.TicksPerSecond;
        ulong remainder = scaledSubsecondTicks % TimeSpan.TicksPerSecond;
        ulong roundedDown = checked(wholeWorldTicks + additionalWorldTicks);

        if (remainder == 0)
        {
            duration = new WorldDuration(roundedDown);
            return TimeSpanWorldDurationResult.Exact;
        }

        switch (rounding)
        {
            case WorldDurationRounding.RequireExact:
                duration = WorldDuration.Zero;
                return TimeSpanWorldDurationResult.UnrepresentableRemainder;
            case WorldDurationRounding.Down:
                duration = new WorldDuration(roundedDown);
                return TimeSpanWorldDurationResult.RoundedDown;
            case WorldDurationRounding.Up when roundedDown != ulong.MaxValue:
                duration = new WorldDuration(checked(roundedDown + 1));
                return TimeSpanWorldDurationResult.RoundedUp;
            case WorldDurationRounding.Up:
                duration = WorldDuration.Zero;
                return TimeSpanWorldDurationResult.OutOfRange;
            default:
                throw new InvalidOperationException("Validated world-duration rounding became undefined.");
        }
    }
}

/// <summary>
/// The explicitly selected loss policy when converting an arbitrary duration to the 60 TPS grid.
/// </summary>
public enum WorldDurationRounding : byte
{
    /// <summary>
    /// Reject any duration that is not an exact multiple of one world tick.
    /// </summary>
    RequireExact = 0,

    /// <summary>
    /// Drop the fractional world tick, so the converted duration never exceeds the source duration.
    /// </summary>
    Down = 1,

    /// <summary>
    /// Add one world tick for a nonzero fractional remainder, so the converted duration never precedes the source duration.
    /// </summary>
    Up = 2,
}

/// <summary>
/// The outcome of converting a <see cref="TimeSpan"/> to an exact or explicitly rounded <see cref="WorldDuration"/>.
/// </summary>
public enum TimeSpanWorldDurationResult : byte
{
    /// <summary>
    /// The source duration was exactly on the 60 TPS grid.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// The source duration was rounded down according to the requested policy.
    /// </summary>
    RoundedDown = 1,

    /// <summary>
    /// The source duration was rounded up according to the requested policy.
    /// </summary>
    RoundedUp = 2,

    /// <summary>
    /// The source duration is not exactly representable and exact conversion was required.
    /// </summary>
    UnrepresentableRemainder = 3,

    /// <summary>
    /// Negative durations are not valid world-clock durations.
    /// </summary>
    NegativeInput = 4,

    /// <summary>
    /// The requested rounded result would exceed the world-duration range.
    /// </summary>
    OutOfRange = 5,

    /// <summary>
    /// The caller supplied an unknown rounding policy.
    /// </summary>
    InvalidRounding = 6,
}
