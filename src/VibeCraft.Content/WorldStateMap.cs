using System.Collections.Immutable;

namespace VibeCraft.Content;

/// <summary>Binds one immutable world-local identifier to its canonical block-state identity.</summary>
public readonly record struct WorldStateBinding
{
    /// <summary>Initializes a validated world-state binding.</summary>
    public WorldStateBinding(WorldStateId id, CanonicalBlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Id = id;
        State = state;
    }

    /// <summary>Gets the world-local identifier.</summary>
    public WorldStateId Id { get; }

    /// <summary>Gets the bound canonical state.</summary>
    public CanonicalBlockState State { get; }

    internal void ThrowIfInvalid()
    {
        if (State is null)
        {
            throw new InvalidOperationException("WorldStateBinding is uninitialized or invalid.");
        }
    }
}

/// <summary>Classifies a reconciliation refusal.</summary>
public enum WorldStateReconciliationError
{
    /// <summary>No further <see cref="WorldStateId"/> can be allocated without wrapping.</summary>
    IdExhausted,
}

/// <summary>Contains either an immutable reconciled map or its non-mutating refusal.</summary>
public sealed class WorldStateReconciliation
{
    private WorldStateReconciliation(WorldStateMap? mapping, WorldStateReconciliationError? error)
    {
        Mapping = mapping;
        Error = error;
    }

    /// <summary>Gets the reconciled mapping when allocation succeeded.</summary>
    public WorldStateMap? Mapping { get; }

    /// <summary>Gets the refusal when allocation could not complete.</summary>
    public WorldStateReconciliationError? Error { get; }

    /// <summary>Gets whether reconciliation succeeded.</summary>
    public bool Success => Mapping is not null;

    internal static WorldStateReconciliation Completed(WorldStateMap mapping)
    {
        return new WorldStateReconciliation(mapping, null);
    }

    internal static WorldStateReconciliation Refused(WorldStateReconciliationError error)
    {
        return new WorldStateReconciliation(null, error);
    }
}

/// <summary>Defines an immutable, append-only, world-local block-state mapping.</summary>
public sealed class WorldStateMap
{
    private readonly ImmutableDictionary<WorldStateId, CanonicalBlockState> statesById;
    private readonly ImmutableDictionary<CanonicalBlockState, WorldStateId> idsByState;

    /// <summary>
    /// The maximum number of canonical states in one world-local mapping, including the mandatory air state at ID zero.
    /// </summary>
    public const int MaxTotalStates = 1_048_576;

    private WorldStateMap(IEnumerable<WorldStateBinding> bindings)
    {
        ImmutableDictionary<WorldStateId, CanonicalBlockState>.Builder stateBuilder = ImmutableDictionary.CreateBuilder<WorldStateId, CanonicalBlockState>();
        ImmutableDictionary<CanonicalBlockState, WorldStateId>.Builder idBuilder = ImmutableDictionary.CreateBuilder<CanonicalBlockState, WorldStateId>();

        foreach (WorldStateBinding binding in bindings)
        {
            if (stateBuilder.Count == MaxTotalStates)
            {
                throw new ArgumentOutOfRangeException(nameof(bindings), $"A world-state map may contain at most {MaxTotalStates} states including air.");
            }

            binding.ThrowIfInvalid();
            if (!stateBuilder.TryAdd(binding.Id, binding.State))
            {
                throw new ArgumentException($"World state ID {binding.Id.Value} is bound more than once.", nameof(bindings));
            }

            if (!idBuilder.TryAdd(binding.State, binding.Id))
            {
                throw new ArgumentException($"Block state {binding.State} is bound more than once.", nameof(bindings));
            }
        }

        if (!stateBuilder.TryGetValue(new WorldStateId(0), out CanonicalBlockState? zeroState) || !zeroState.Equals(CanonicalBlockState.Air))
        {
            throw new ArgumentException("WorldStateId 0 must be bound exactly to vibecraft:air.", nameof(bindings));
        }

        statesById = stateBuilder.ToImmutable();
        idsByState = idBuilder.ToImmutable();
    }

    /// <summary>Gets the empty world mapping, containing only <c>vibecraft:air</c> at ID zero.</summary>
    public static WorldStateMap Empty { get; } = new([new WorldStateBinding(new WorldStateId(0), CanonicalBlockState.Air)]);

    /// <summary>Gets all bindings in ascending world-state ID order.</summary>
    public ImmutableArray<WorldStateBinding> Bindings =>
        [.. statesById.OrderBy(pair => pair.Key.Value).Select(pair => new WorldStateBinding(pair.Key, pair.Value))];

    /// <summary>Restores and validates an immutable mapping supplied by a future storage boundary.</summary>
    public static WorldStateMap Restore(IEnumerable<WorldStateBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        return bindings.TryGetNonEnumeratedCount(out int count) && count > MaxTotalStates
            ? throw new ArgumentOutOfRangeException(nameof(bindings), $"A world-state map may contain at most {MaxTotalStates} states including air.")
            : new WorldStateMap(bindings);
    }

    /// <summary>Attempts to get a world-local ID for a canonical state.</summary>
    public bool TryGetId(CanonicalBlockState state, out WorldStateId id)
    {
        ArgumentNullException.ThrowIfNull(state);
        return idsByState.TryGetValue(state, out id);
    }

    /// <summary>Attempts to get the state bound to a world-local ID.</summary>
    public bool TryGetState(WorldStateId id, out CanonicalBlockState? state)
    {
        return statesById.TryGetValue(id, out state);
    }

    /// <summary>
    /// Produces a new mapping that retains every prior binding and allocates new IDs in canonical-state order.
    /// This operation has no side effects and never wraps or reuses a prior ID.
    /// </summary>
    public WorldStateReconciliation Reconcile(IEnumerable<CanonicalBlockState> discoveredStates)
    {
        ArgumentNullException.ThrowIfNull(discoveredStates);

        if (discoveredStates.TryGetNonEnumeratedCount(out int suppliedCount) && suppliedCount > MaxTotalStates)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discoveredStates),
                $"Reconciliation may inspect at most {MaxTotalStates} discovered states; the resulting total includes air.");
        }

        HashSet<CanonicalBlockState> missingSet = [];
        int inspectedCount = 0;
        foreach (CanonicalBlockState state in discoveredStates)
        {
            if (inspectedCount == MaxTotalStates)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(discoveredStates),
                    $"Reconciliation may inspect at most {MaxTotalStates} discovered states; the resulting total includes air.");
            }

            inspectedCount++;
            if (state is null)
            {
                throw new ArgumentException("Discovered states cannot contain null entries.", nameof(discoveredStates));
            }

            if (state.Equals(CanonicalBlockState.Air) || idsByState.ContainsKey(state) || !missingSet.Add(state))
            {
                continue;
            }

            if (missingSet.Count > MaxTotalStates - statesById.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(discoveredStates),
                    $"A reconciled world-state map may contain at most {MaxTotalStates} states including air.");
            }
        }

        CanonicalBlockState[] missing = [.. missingSet.OrderBy(state => state)];

        uint next = statesById.Keys.Max(id => id.Value);
        if (missing.Length > 0 && next == uint.MaxValue)
        {
            return WorldStateReconciliation.Refused(WorldStateReconciliationError.IdExhausted);
        }

        List<WorldStateBinding> reconciled = [.. Bindings];
        foreach (CanonicalBlockState state in missing)
        {
            if (next == uint.MaxValue)
            {
                return WorldStateReconciliation.Refused(WorldStateReconciliationError.IdExhausted);
            }

            next++;
            reconciled.Add(new WorldStateBinding(new WorldStateId(next), state));
        }

        return WorldStateReconciliation.Completed(new WorldStateMap(reconciled));
    }
}

/// <summary>Defines a deterministic, immutable runtime projection of resolved world states.</summary>
public sealed class RuntimeStateMap
{
    private readonly ImmutableDictionary<WorldStateId, RuntimeStateId> runtimeByWorld;
    private readonly ImmutableDictionary<RuntimeStateId, CanonicalBlockState> stateByRuntime;

    private RuntimeStateMap(
        ImmutableDictionary<WorldStateId, RuntimeStateId> runtimeByWorld,
        ImmutableDictionary<RuntimeStateId, CanonicalBlockState> stateByRuntime)
    {
        this.runtimeByWorld = runtimeByWorld;
        this.stateByRuntime = stateByRuntime;
    }

    /// <summary>Builds a dense runtime projection in canonical-state order, independent of discovery order.</summary>
    public static RuntimeStateMap Create(WorldStateMap worldStates, IEnumerable<CanonicalBlockState> resolvedStates)
    {
        ArgumentNullException.ThrowIfNull(worldStates);
        ArgumentNullException.ThrowIfNull(resolvedStates);

        if (resolvedStates.TryGetNonEnumeratedCount(out int suppliedCount) && suppliedCount > WorldStateMap.MaxTotalStates)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedStates),
                $"Runtime projection may inspect at most {WorldStateMap.MaxTotalStates} resolved states; the total includes air.");
        }

        HashSet<CanonicalBlockState> resolved = [CanonicalBlockState.Air];
        int inspectedCount = 0;
        foreach (CanonicalBlockState state in resolvedStates)
        {
            if (inspectedCount == WorldStateMap.MaxTotalStates)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resolvedStates),
                    $"Runtime projection may inspect at most {WorldStateMap.MaxTotalStates} resolved states; the total includes air.");
            }

            inspectedCount++;
            if (state is null)
            {
                throw new ArgumentException("Resolved states cannot contain null entries.", nameof(resolvedStates));
            }

            if (resolved.Add(state) && resolved.Count > WorldStateMap.MaxTotalStates)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resolvedStates),
                    $"A runtime state map may contain at most {WorldStateMap.MaxTotalStates} states including air.");
            }
        }

        CanonicalBlockState[] ordered = [.. resolved.Where(state => !state.Equals(CanonicalBlockState.Air)).OrderBy(state => state)];
        ImmutableDictionary<WorldStateId, RuntimeStateId>.Builder runtimeBuilder = ImmutableDictionary.CreateBuilder<WorldStateId, RuntimeStateId>();
        ImmutableDictionary<RuntimeStateId, CanonicalBlockState>.Builder stateBuilder = ImmutableDictionary.CreateBuilder<RuntimeStateId, CanonicalBlockState>();
        if (!worldStates.TryGetId(CanonicalBlockState.Air, out WorldStateId airWorldId))
        {
            throw new ArgumentException("World-state maps must contain vibecraft:air.", nameof(worldStates));
        }

        runtimeBuilder.Add(airWorldId, new RuntimeStateId(0));
        stateBuilder.Add(new RuntimeStateId(0), CanonicalBlockState.Air);
        uint runtimeId = 1;

        foreach (CanonicalBlockState state in ordered)
        {
            if (!worldStates.TryGetId(state, out WorldStateId worldId))
            {
                continue;
            }

            RuntimeStateId id = new(runtimeId++);
            runtimeBuilder.Add(worldId, id);
            stateBuilder.Add(id, state);
        }

        return new RuntimeStateMap(runtimeBuilder.ToImmutable(), stateBuilder.ToImmutable());
    }

    /// <summary>Attempts to resolve a world-local ID to a runtime ID.</summary>
    public bool TryResolve(WorldStateId worldId, out RuntimeStateId runtimeId)
    {
        return runtimeByWorld.TryGetValue(worldId, out runtimeId);
    }

    /// <summary>Attempts to describe a runtime ID.</summary>
    public bool TryDescribe(RuntimeStateId runtimeId, out CanonicalBlockState? state)
    {
        return stateByRuntime.TryGetValue(runtimeId, out state);
    }
}
