namespace VibeCraft.Simulation.Abstractions.Phases;

/// <summary>
/// The stable owner/action/publication barriers of one authoritative world tick.
/// </summary>
/// <remarks>
/// This is vocabulary only. It does not schedule work or implement a simulation loop.
/// </remarks>
public enum WorldTickPhase : byte
{
    /// <summary>
    /// The owner accepts validated external completions and applies boundary transitions.
    /// </summary>
    OwnerStart = 0,

    /// <summary>
    /// The owner applies validated inputs and deterministic world actions.
    /// </summary>
    Actions = 1,

    /// <summary>
    /// The owner resolves deferred changes and finalizes authoritative revisions.
    /// </summary>
    OwnerCommit = 2,

    /// <summary>
    /// Immutable network, persistence, and replay observations may be produced.
    /// </summary>
    Publication = 3,
}
