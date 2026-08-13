using FsCheck.Xunit;
using VibeCraft.Content;
using Xunit;

namespace VibeCraft.G1.Tests.Content;

public sealed class WorldStateMapTests
{
    [Fact]
    public void EmptyMapBindsZeroExactlyToAir()
    {
        Assert.True(WorldStateMap.Empty.TryGetState(new WorldStateId(0), out CanonicalBlockState? state));
        Assert.Equal(CanonicalBlockState.Air, state);
        Assert.True(WorldStateMap.Empty.TryGetId(CanonicalBlockState.Air, out WorldStateId id));
        Assert.Equal(new WorldStateId(0), id);

        _ = Assert.Throws<ArgumentException>(() => WorldStateMap.Restore(
            [new WorldStateBinding(new WorldStateId(0), State("vibecraft:stone"))]));
        _ = Assert.Throws<InvalidOperationException>(() => WorldStateMap.Restore([default]));
    }

    [Fact]
    public void ReconciliationIsIndependentOfDiscoveryOrderAndNeverChangesPriorIds()
    {
        CanonicalBlockState stone = State("vibecraft:stone");
        CanonicalBlockState grass = State("vibecraft:grass_block");
        CanonicalBlockState log = new(
            ContentKey.Parse("vibecraft:oak_log"),
            [BlockStateProperty.Create(ContentKey.Parse("vibecraft:axis"), "y")]);
        WorldStateMap prior = WorldStateMap.Restore(
            [
                new WorldStateBinding(new WorldStateId(0), CanonicalBlockState.Air),
                new WorldStateBinding(new WorldStateId(7), stone),
            ]);

        WorldStateReconciliation first = prior.Reconcile([log, grass, stone]);
        WorldStateReconciliation second = prior.Reconcile([stone, grass, log]);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(BindingProjection(first.Mapping!), BindingProjection(second.Mapping!));
        WorldStateMap reconciled = first.Mapping!;
        Assert.Equal(new WorldStateId(7), GetId(reconciled, stone));
        Assert.Equal(new WorldStateId(8), GetId(reconciled, grass));
        Assert.Equal(new WorldStateId(9), GetId(reconciled, log));
        Assert.Equal(new WorldStateId(7), GetId(prior, stone));
    }

    [Property(MaxTest = 100)]
    public void ReconciliationRemainsStableForShuffledSyntheticDiscovery(uint seed)
    {
        CanonicalBlockState[] ordered = [.. Enumerable.Range(0, 12).Select(index => State($"mod{index % 3}:state_{index}"))];
        CanonicalBlockState[] shuffled = [.. ordered.OrderBy(state => DeterministicRank(state, seed))];

        WorldStateReconciliation expected = WorldStateMap.Empty.Reconcile(ordered);
        WorldStateReconciliation actual = WorldStateMap.Empty.Reconcile(shuffled);

        Assert.True(expected.Success);
        Assert.True(actual.Success);
        Assert.Equal(BindingProjection(expected.Mapping!), BindingProjection(actual.Mapping!));
    }

    [Fact]
    public void ExhaustionIsANonMutatingErrorRatherThanWrapOrReuse()
    {
        CanonicalBlockState retained = State("vibecraft:retained");
        WorldStateMap full = WorldStateMap.Restore(
            [
                new WorldStateBinding(new WorldStateId(0), CanonicalBlockState.Air),
                new WorldStateBinding(new WorldStateId(uint.MaxValue), retained),
            ]);

        WorldStateReconciliation result = full.Reconcile([retained, State("vibecraft:new_state")]);

        Assert.False(result.Success);
        Assert.Null(result.Mapping);
        Assert.Equal(WorldStateReconciliationError.IdExhausted, result.Error);
        Assert.Equal(new WorldStateId(uint.MaxValue), GetId(full, retained));
    }

    [Fact]
    public void RuntimeProjectionIsDenseAndIndependentOfResolvedStateDiscoveryOrder()
    {
        CanonicalBlockState stone = State("vibecraft:stone");
        CanonicalBlockState grass = State("vibecraft:grass_block");
        WorldStateMap map = WorldStateMap.Empty.Reconcile([stone, grass]).Mapping!;

        RuntimeStateMap first = RuntimeStateMap.Create(map, [stone, grass]);
        RuntimeStateMap second = RuntimeStateMap.Create(map, [grass, stone]);

        Assert.Equal(new RuntimeStateId(0), Resolve(first, new WorldStateId(0)));
        Assert.Equal(Resolve(first, GetId(map, grass)), Resolve(second, GetId(map, grass)));
        Assert.Equal(Resolve(first, GetId(map, stone)), Resolve(second, GetId(map, stone)));
    }

    [Fact]
    public void NullStateEntriesAreRejectedAtRestoreReconciliationAndRuntimeBoundaries()
    {
        WorldStateMap map = WorldStateMap.Empty;
        IEnumerable<CanonicalBlockState> nullState = [null!];

        _ = Assert.Throws<ArgumentException>(() => map.Reconcile(nullState));
        _ = Assert.Throws<ArgumentException>(() => RuntimeStateMap.Create(map, nullState));
        _ = Assert.Throws<ArgumentNullException>(() => new WorldStateBinding(new WorldStateId(1), null!));
    }

    [Fact]
    public void TotalStateCeilingIncludesAirAndBoundsRestoreBeforeEnumeration()
    {
        WorldStateBinding air = new(new WorldStateId(0), CanonicalBlockState.Air);
        CountedCollection<WorldStateBinding> tooMany = new(air, WorldStateMap.MaxTotalStates + 1);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => WorldStateMap.Restore(tooMany));

        _ = Assert.Single(WorldStateMap.Empty.Bindings);
        Assert.Equal(0, tooMany.YieldCount);
    }

    [Fact]
    public void ReconciliationAndRuntimeProjectionStopStreamingAtTheTotalStateCeiling()
    {
        CanonicalBlockState stone = State("vibecraft:stone");
        WorldStateMap map = WorldStateMap.Empty.Reconcile([stone]).Mapping!;
        CountingRepeatEnumerable<CanonicalBlockState> reconcileInput = new(stone, WorldStateMap.MaxTotalStates + 1);
        CountingRepeatEnumerable<CanonicalBlockState> runtimeInput = new(stone, WorldStateMap.MaxTotalStates + 1);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => map.Reconcile(reconcileInput));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeStateMap.Create(map, runtimeInput));

        Assert.Equal(WorldStateMap.MaxTotalStates + 1, reconcileInput.YieldCount);
        Assert.Equal(WorldStateMap.MaxTotalStates + 1, runtimeInput.YieldCount);
    }

    [Property(MaxTest = 100)]
    public void EndToEndStateIdentityRoundTripIsDeterministicUnderShuffle(uint seed)
    {
        CanonicalBlockState[] states =
        [
            .. Enumerable.Range(0, 24).Select(index => new CanonicalBlockState(
                ContentKey.Parse($"mod_{seed % 31}:state_{index}"),
                [
                    BlockStateProperty.Create(
                        ContentKey.Parse("vibecraft:variant"),
                        $"v_{(seed + (uint)index) % 7}"),
                ])),
        ];
        CanonicalBlockState[] shuffled = [.. states.OrderBy(state => DeterministicRank(state, seed))];
        WorldStateMap orderedWorld = WorldStateMap.Empty.Reconcile(states).Mapping!;
        WorldStateMap shuffledWorld = WorldStateMap.Empty.Reconcile(shuffled).Mapping!;
        WorldStateMap restored = WorldStateMap.Restore(shuffledWorld.Bindings.Reverse());
        RuntimeStateMap orderedRuntime = RuntimeStateMap.Create(restored, states);
        RuntimeStateMap shuffledRuntime = RuntimeStateMap.Create(restored, shuffled);

        Assert.Equal(BindingProjection(orderedWorld), BindingProjection(shuffledWorld));
        foreach (CanonicalBlockState state in states)
        {
            WorldStateId worldId = GetId(restored, state);
            Assert.True(restored.TryGetState(worldId, out CanonicalBlockState? restoredState));
            Assert.Equal(state, restoredState);

            RuntimeStateId orderedRuntimeId = Resolve(orderedRuntime, worldId);
            RuntimeStateId shuffledRuntimeId = Resolve(shuffledRuntime, worldId);
            Assert.Equal(orderedRuntimeId, shuffledRuntimeId);
            Assert.Equal(state, Describe(orderedRuntime, orderedRuntimeId));
        }
    }

    private static CanonicalBlockState State(string key)
    {
        return new CanonicalBlockState(ContentKey.Parse(key), []);
    }

    private static WorldStateId GetId(WorldStateMap map, CanonicalBlockState state)
    {
        Assert.True(map.TryGetId(state, out WorldStateId id));
        return id;
    }

    private static RuntimeStateId Resolve(RuntimeStateMap map, WorldStateId id)
    {
        Assert.True(map.TryResolve(id, out RuntimeStateId runtimeId));
        return runtimeId;
    }

    private static CanonicalBlockState Describe(RuntimeStateMap map, RuntimeStateId id)
    {
        Assert.True(map.TryDescribe(id, out CanonicalBlockState? state));
        return Assert.IsType<CanonicalBlockState>(state);
    }

    private static IEnumerable<(WorldStateId Id, string State)> BindingProjection(WorldStateMap map)
    {
        return map.Bindings.Select(binding => (binding.Id, binding.State.ToString()));
    }

    private static uint DeterministicRank(CanonicalBlockState state, uint seed)
    {
        uint hash = 2_166_136_261;
        foreach (char value in state.ToString())
        {
            hash = (hash ^ value) * 16_777_619;
        }

        return hash ^ seed;
    }

    private sealed class CountingRepeatEnumerable<T>(T value, int count) : IEnumerable<T>
    {
        public int YieldCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            for (int index = 0; index < count; index++)
            {
                YieldCount++;
                yield return value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class CountedCollection<T>(T value, int count) : ICollection<T>
    {
        public int Count => count;

        public bool IsReadOnly => true;

        public int YieldCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            for (int index = 0; index < count; index++)
            {
                YieldCount++;
                yield return value;
            }
        }

        public bool Contains(T item)
        {
            return EqualityComparer<T>.Default.Equals(value, item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
