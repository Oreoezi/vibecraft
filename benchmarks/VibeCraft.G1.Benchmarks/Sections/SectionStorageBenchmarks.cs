using BenchmarkDotNet.Attributes;
using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId)]
public class SectionStorageReadBenchmarks
{
    private BlockStateId[] _dense = null!;
    private MutableSectionBlockStates _section = null!;
    private BlockStateId[] _projection = null!;
    private int[] _randomTrace = null!;
    private LocalBlock[] _randomLocals = null!;
    private LocalBlock[] _linearLocals = null!;

    [Params(16, 32)]
    public int Side { get; set; }

    [Params("UniformAir", "UniformStone", "Layered", "Mixed", "HighEntropy")]
    public string Fixture { get; set; } = "Mixed";

    [GlobalSetup]
    public void Setup()
    {
        const ulong seed = SectionCandidateFixture.DefaultSeed;
        SectionBenchmarkSupport.EmitObservationManifestOnce(seed, "Release");
        SectionGeometry geometry = new(new SectionSide(Side));
        _dense = SectionCandidateFixture.CreateStates(geometry, SectionBenchmarkSupport.ParseFixture(Fixture), seed);
        _section = SectionCandidateFixture.CreateSection(geometry, _dense);
        _projection = new BlockStateId[_dense.Length];
        _section.CopyTo(_projection);
        if (!_projection.AsSpan().SequenceEqual(_dense))
        {
            throw new InvalidOperationException("Per-section adaptive fixture differs from its dense baseline.");
        }

        _randomTrace = SectionBenchmarkSupport.CreateRandomTrace(
            SectionBenchmarkSupport.RandomTraceLength,
            _dense.Length,
            seed ^ 0xA0761D6478BD642FUL);
        _randomLocals = new LocalBlock[_randomTrace.Length];
        for (int index = 0; index < _randomTrace.Length; index++)
        {
            _randomLocals[index] = SectionCandidateFixture.ToLocal(new LocalIndex(_randomTrace[index]), geometry);
        }

        _linearLocals = new LocalBlock[_dense.Length];
        for (int index = 0; index < _linearLocals.Length; index++)
        {
            _linearLocals[index] = SectionCandidateFixture.ToLocal(new LocalIndex(index), geometry);
        }
    }

    [Benchmark]
    [BenchmarkCategory("SectionRandomReads")]
    public ulong AdaptiveRandomReads()
    {
        ulong checksum = 0;
        foreach (LocalBlock local in _randomLocals)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ _section.Get(local).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("SectionRandomReads")]
    public ulong DenseRandomReads()
    {
        ulong checksum = 0;
        foreach (int index in _randomTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ _dense[index].Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("SectionLinearReads")]
    public ulong AdaptiveLinearReads()
    {
        ulong checksum = 0;
        for (int index = 0; index < _section.Count; index++)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ _section.Get(_linearLocals[index]).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("SectionLinearReads")]
    public ulong DenseLinearReads()
    {
        return SectionBenchmarkSupport.Checksum(_dense);
    }

    [Benchmark]
    [BenchmarkCategory("SectionSnapshots")]
    public ulong CaptureSnapshot()
    {
        SectionBlockStateSnapshot snapshot = _section.CaptureSnapshot();
        return checked((ulong)snapshot.Revision.Value) ^ checked((ulong)snapshot.GetStorageMetrics().KnownPayloadBytes);
    }

    [Benchmark]
    [BenchmarkCategory("SectionProjection")]
    public ulong LogicalProjectionCopyTo()
    {
        _section.CopyTo(_projection);
        return SectionBenchmarkSupport.Checksum(_projection);
    }
}

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId, "SectionEdits")]
public class SectionStorageEditBenchmarks
{
    private const int ClusterCount = 64;
    private BlockStateId[] _canonical = null!;
    private BlockStateId[] _denseCandidate = null!;
    private MutableSectionBlockStates _adaptiveCandidate = null!;
    private LocalSectionEdit[] _trace = null!;
    private LocalBlock[] _locals = null!;

    [Params(16, 32)]
    public int Side { get; set; }

    [Params("Mixed", "HighEntropy")]
    public string Fixture { get; set; } = "Mixed";

    [GlobalSetup]
    public void GlobalSetup()
    {
        const ulong seed = SectionCandidateFixture.DefaultSeed;
        SectionBenchmarkSupport.EmitObservationManifestOnce(seed, "Release");
        SectionGeometry geometry = new(new SectionSide(Side));
        _canonical = SectionCandidateFixture.CreateStates(geometry, SectionBenchmarkSupport.ParseFixture(Fixture), seed);
        _trace = CreateClusteredTrace(_canonical, Side, seed);
        _locals = new LocalBlock[_trace.Length];
        bool hasNoOp = false;
        bool hasExistingStateChange = false;
        bool hasNewStateChange = false;
        for (int index = 0; index < _trace.Length; index++)
        {
            _locals[index] = SectionCandidateFixture.ToLocal(_trace[index].Index, geometry);
            hasNoOp |= _trace[index].Intent == SectionEditIntent.NoOp;
            hasExistingStateChange |= _trace[index].Intent == SectionEditIntent.ExistingStateChange;
            hasNewStateChange |= _trace[index].Intent == SectionEditIntent.NewStateChange;
        }

        if (!hasNoOp || !hasExistingStateChange || !hasNewStateChange)
        {
            throw new InvalidOperationException("The local clustered edit benchmark must contain no-ops, existing-state changes, and new-state changes.");
        }

        ValidateTraceEquivalence(geometry);
    }

    [IterationSetup(Target = nameof(AdaptiveClusteredEdits))]
    public void SetupAdaptive()
    {
        _adaptiveCandidate = SectionCandidateFixture.CreateSection(
            new SectionGeometry(new SectionSide(Side)),
            _canonical);
    }

    [IterationSetup(Target = nameof(DenseClusteredEdits))]
    public void SetupDense()
    {
        _denseCandidate = (BlockStateId[])_canonical.Clone();
    }

    [Benchmark]
    public ulong AdaptiveClusteredEdits()
    {
        ulong checksum = 0xCBF29CE484222325UL;
        for (int index = 0; index < _trace.Length; index++)
        {
            SectionWriteResult result = _adaptiveCandidate.TrySet(_locals[index], _trace[index].State);
            checksum = AddLocalEditChecksum(checksum, _trace[index], result);
        }

        return checksum;
    }

    [Benchmark(Baseline = true)]
    public ulong DenseClusteredEdits()
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (LocalSectionEdit edit in _trace)
        {
            SectionWriteResult result = SetDense(_denseCandidate, edit);
            checksum = AddLocalEditChecksum(checksum, edit, result);
        }

        return checksum;
    }

    private static LocalSectionEdit[] CreateClusteredTrace(
        ReadOnlySpan<BlockStateId> canonical,
        int side,
        ulong seed)
    {
        const int clusterSide = 4;
        const int editsPerCluster = clusterSide * clusterSide * clusterSide;
        BlockStateId[] working = canonical.ToArray();
        BlockStateId[] existingStates = [.. canonical.ToArray().Distinct().OrderBy(state => state.Value)];
        SectionGeometry geometry = new(new SectionSide(side));
        LocalSectionEdit[] trace = new LocalSectionEdit[ClusterCount * editsPerCluster];
        ulong random = seed ^ checked((uint)side) ^ 0xD1B54A32D192ED03UL;
        int originRange = side - clusterSide + 1;
        int traceIndex = 0;
        for (int cluster = 0; cluster < ClusterCount; cluster++)
        {
            int originX = checked((int)(SectionBenchmarkSupport.Next(ref random) % checked((uint)originRange)));
            int originY = checked((int)(SectionBenchmarkSupport.Next(ref random) % checked((uint)originRange)));
            int originZ = checked((int)(SectionBenchmarkSupport.Next(ref random) % checked((uint)originRange)));
            for (int localY = 0; localY < clusterSide; localY++)
            {
                for (int localZ = 0; localZ < clusterSide; localZ++)
                {
                    for (int localX = 0; localX < clusterSide; localX++)
                    {
                        int x = originX + localX;
                        int y = originY + localY;
                        int z = originZ + localZ;
                        LocalIndex localIndex = geometry.GetLocalIndex(geometry.CreateLocal(x, y, z));
                        SectionEditIntent requestedIntent = (traceIndex % 4) switch
                        {
                            0 or 3 => SectionEditIntent.NoOp,
                            1 => SectionEditIntent.ExistingStateChange,
                            _ => SectionEditIntent.NewStateChange,
                        };
                        BlockStateId state = SelectEditState(
                            working[localIndex.Value],
                            existingStates,
                            requestedIntent,
                            traceIndex,
                            out SectionEditIntent actualIntent);
                        trace[traceIndex] = new LocalSectionEdit(localIndex, state, actualIntent);
                        if (actualIntent != SectionEditIntent.NoOp)
                        {
                            working[localIndex.Value] = state;
                        }

                        traceIndex++;
                    }
                }
            }
        }

        return trace;
    }

    private static BlockStateId SelectEditState(
        BlockStateId current,
        BlockStateId[] existingStates,
        SectionEditIntent requestedIntent,
        int traceIndex,
        out SectionEditIntent actualIntent)
    {
        if (requestedIntent == SectionEditIntent.NoOp)
        {
            actualIntent = requestedIntent;
            return current;
        }

        if (requestedIntent == SectionEditIntent.ExistingStateChange)
        {
            int start = traceIndex % existingStates.Length;
            for (int offset = 0; offset < existingStates.Length; offset++)
            {
                BlockStateId candidate = existingStates[(start + offset) % existingStates.Length];
                if (!candidate.Equals(current))
                {
                    actualIntent = requestedIntent;
                    return candidate;
                }
            }
        }

        actualIntent = SectionEditIntent.NewStateChange;
        return new BlockStateId(checked(0x80000000U + (uint)traceIndex));
    }

    private static SectionWriteResult SetDense(Span<BlockStateId> dense, LocalSectionEdit edit)
    {
        if (dense[edit.Index.Value].Equals(edit.State))
        {
            return SectionWriteResult.Unchanged;
        }

        dense[edit.Index.Value] = edit.State;
        return SectionWriteResult.Changed;
    }

    private static ulong AddLocalEditChecksum(
        ulong checksum,
        LocalSectionEdit edit,
        SectionWriteResult result)
    {
        checksum = unchecked((checksum ^ checked((uint)edit.Index.Value)) * 0x100000001B3UL);
        checksum = unchecked((checksum ^ edit.State.Value) * 0x100000001B3UL);
        return unchecked((checksum ^ (uint)result) * 0x100000001B3UL);
    }

    private void ValidateTraceEquivalence(SectionGeometry geometry)
    {
        BlockStateId[] dense = (BlockStateId[])_canonical.Clone();
        MutableSectionBlockStates adaptive = SectionCandidateFixture.CreateSection(geometry, _canonical);
        for (int index = 0; index < _trace.Length; index++)
        {
            SectionWriteResult adaptiveResult = adaptive.TrySet(_locals[index], _trace[index].State);
            SectionWriteResult denseResult = SetDense(dense, _trace[index]);
            if (adaptiveResult != denseResult)
            {
                throw new InvalidOperationException($"Adaptive/dense local edit result mismatch at local index {_trace[index].Index.Value}.");
            }
        }

        BlockStateId[] projection = new BlockStateId[_canonical.Length];
        adaptive.CopyTo(projection);
        if (!projection.AsSpan().SequenceEqual(dense))
        {
            throw new InvalidOperationException("Adaptive/dense local edit final states differ.");
        }
    }

    private readonly record struct LocalSectionEdit(
        LocalIndex Index,
        BlockStateId State,
        SectionEditIntent Intent);
}

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId, "SectionGrowth")]
public class SectionPaletteGrowthBenchmarks
{
    private MutableSectionBlockStates _candidate = null!;
    private SectionGeometry _geometry;
    private LocalBlock[] _growthLocals = null!;

    [Params(16, 32)]
    public int Side { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _geometry = new SectionGeometry(new SectionSide(Side));
        _growthLocals = new LocalBlock[257];
        for (int index = 1; index <= 256; index++)
        {
            _growthLocals[index] = _geometry.GetLocalBlock(new LocalIndex(index));
        }
    }

    [IterationSetup]
    public void Setup()
    {
        SectionBenchmarkSupport.EmitObservationManifestOnce(SectionCandidateFixture.DefaultSeed, "Release");
        _candidate = new MutableSectionBlockStates(
            _geometry,
            default,
            default);
    }

    [Benchmark]
    public ulong PaletteGrowthRepackAndDirectPromotion()
    {
        ulong checksum = 0;
        for (int index = 1; index <= 256; index++)
        {
            SectionWriteResult result = _candidate.TrySet(
                _growthLocals[index],
                new BlockStateId(checked((uint)index)));
            checksum = unchecked((checksum * 31UL) + (uint)result);
        }

        SectionStorageMetrics metrics = _candidate.GetStorageMetrics();
        return checksum ^ checked((ulong)metrics.KnownPayloadBytes) ^ (uint)metrics.Kind;
    }
}
