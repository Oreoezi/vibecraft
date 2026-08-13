namespace VibeCraft.Simulation.Abstractions.Phases;

/// <summary>
/// Explicit stable ordering for <see cref="WorldTickPhase"/>.
/// </summary>
public static class WorldTickPhaseOrder
{
    /// <summary>
    /// Gets the authoritative phases in their required order.
    /// </summary>
    public static ReadOnlySpan<WorldTickPhase> InOrder =>
    [
        WorldTickPhase.OwnerStart,
        WorldTickPhase.Actions,
        WorldTickPhase.OwnerCommit,
        WorldTickPhase.Publication,
    ];

    /// <summary>
    /// Determines whether <paramref name="first"/> precedes <paramref name="second"/>.
    /// </summary>
    /// <param name="first">The earlier candidate phase.</param>
    /// <param name="second">The later candidate phase.</param>
    /// <returns><see langword="true"/> only when <paramref name="first"/> precedes <paramref name="second"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="first"/> or <paramref name="second"/> is undefined.</exception>
    public static bool IsBefore(WorldTickPhase first, WorldTickPhase second)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            Enum.IsDefined(first),
            true,
            nameof(first));
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            Enum.IsDefined(second),
            true,
            nameof(second));

        return (byte)first < (byte)second;
    }
}
