using BenchmarkDotNet.Attributes;
using VibeCraft.Content;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId)]
public class EqualVolumeSectionReadBenchmarks
{
    private BlockStateId[] _canonical = null!;
    private SectionEqualVolumeLayout _layout;
    private IReadOnlySectionBlockStates[] _sections = null!;
    private BlockStateId[] _projection = null!;
    private BlockStateId[][] _side16Scratch = null!;
    private int[] _randomTrace = null!;

    [Params("OneSide32", "EightSide16")]
    public string Layout { get; set; } = "OneSide32";

    [Params("UniformAir", "UniformStone", "Layered", "Mixed", "HighEntropy")]
    public string Fixture { get; set; } = "Mixed";

    [GlobalSetup]
    public void Setup()
    {
        const ulong seed = SectionCandidateFixture.DefaultSeed;
        SectionBenchmarkSupport.EmitObservationManifestOnce(seed, "Release");
        _layout = SectionBenchmarkSupport.ParseLayout(Layout);
        _canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionBenchmarkSupport.ParseFixture(Fixture), seed);
        _sections = SectionEqualVolumeFixture.CreateSections(_layout, _canonical);
        SectionEqualVolumeFixture.ValidateSections(_sections, _layout);
        SectionBenchmarkSupport.ValidateEqualWorld(_sections, _layout, _canonical);
        _projection = new BlockStateId[_canonical.Length];
        _side16Scratch = SectionBenchmarkSupport.CreateSide16Scratch();
        SectionEqualVolumeFixture.CopyToCanonical(_sections, _layout, _projection, _side16Scratch);
        _randomTrace = SectionBenchmarkSupport.CreateRandomTrace(
            SectionBenchmarkSupport.RandomTraceLength,
            _canonical.Length,
            seed ^ 0xE7037ED1A0B428DBUL);
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeRandomReads")]
    public ulong AdaptiveRandomReads()
    {
        ulong checksum = 0;
        foreach (int index in _randomTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetGlobalUnchecked(_sections, _layout, index).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeRandomReads")]
    public ulong DenseRandomReads()
    {
        ulong checksum = 0;
        foreach (int index in _randomTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ _canonical[index].Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeLinearReads")]
    public ulong AdaptiveLinearReads()
    {
        ulong checksum = 0;
        for (int index = 0; index < _canonical.Length; index++)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetGlobalUnchecked(_sections, _layout, index).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeLinearReads")]
    public ulong DenseLinearReads()
    {
        return SectionBenchmarkSupport.Checksum(_canonical);
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeSnapshots")]
    public ulong CaptureSnapshots()
    {
        ulong checksum = 0;
        foreach (IReadOnlySectionBlockStates candidate in _sections)
        {
            SectionBlockStateSnapshot snapshot = ((MutableSectionBlockStates)candidate).CaptureSnapshot();
            checksum = unchecked((checksum * 31UL) ^ checked((ulong)snapshot.GetStorageMetrics().KnownPayloadBytes));
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeProjection")]
    public ulong LogicalProjectionCopyTo()
    {
        SectionEqualVolumeFixture.CopyToCanonicalUnchecked(_sections, _layout, _projection, _side16Scratch);
        return SectionBenchmarkSupport.Checksum(_projection);
    }
}

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId, "EqualVolumeEdits")]
public class EqualVolumeSectionEditBenchmarks
{
    private BlockStateId[] _canonical = null!;
    private BlockStateId[] _denseCandidate = null!;
    private SectionEqualVolumeLayout _layout;
    private MutableSectionBlockStates[] _adaptiveCandidate = null!;
    private SectionEdit[] _trace = null!;

    [Params("OneSide32", "EightSide16")]
    public string Layout { get; set; } = "OneSide32";

    [Params("Mixed", "HighEntropy")]
    public string Fixture { get; set; } = "Mixed";

    [Params("InteriorClusters", "BoundaryClusters")]
    public string Trace { get; set; } = "BoundaryClusters";

    [GlobalSetup]
    public void GlobalSetup()
    {
        const ulong seed = SectionCandidateFixture.DefaultSeed;
        SectionBenchmarkSupport.EmitObservationManifestOnce(seed, "Release");
        _layout = SectionBenchmarkSupport.ParseLayout(Layout);
        _canonical = SectionEqualVolumeFixture.CreateCanonicalCube(SectionBenchmarkSupport.ParseFixture(Fixture), seed);
        _trace = SectionEqualVolumeFixture.CreateEditTrace(
            _canonical,
            SectionBenchmarkSupport.ParseTrace(Trace),
            seed,
            SectionEqualVolumeFixture.DefaultClusterCount);
        if (!_trace.Any(edit => edit.Intent == SectionEditIntent.NoOp) ||
            !_trace.Any(edit => edit.Intent == SectionEditIntent.ExistingStateChange))
        {
            throw new InvalidOperationException("The clustered edit benchmark must contain no-ops and existing-state changes.");
        }

        ValidateTraceEquivalence();
    }

    [IterationSetup(Target = nameof(AdaptiveClusteredEdits))]
    public void SetupAdaptive()
    {
        _adaptiveCandidate = SectionEqualVolumeFixture.CreateSections(_layout, _canonical);
        SectionEqualVolumeFixture.ValidateSections(_adaptiveCandidate, _layout);
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
        foreach (SectionEdit edit in _trace)
        {
            SectionWriteResult result = SectionEqualVolumeFixture.SetGlobalUnchecked(_adaptiveCandidate, _layout, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit, result);
        }

        return checksum;
    }

    [Benchmark(Baseline = true)]
    public ulong DenseClusteredEdits()
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (SectionEdit edit in _trace)
        {
            SectionWriteResult result = SetDenseUnchecked(_denseCandidate, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit, result);
        }

        return checksum;
    }

    private void ValidateTraceEquivalence()
    {
        BlockStateId[] dense = (BlockStateId[])_canonical.Clone();
        MutableSectionBlockStates[] adaptive = SectionEqualVolumeFixture.CreateSections(_layout, _canonical);
        foreach (SectionEdit edit in _trace)
        {
            SectionWriteResult denseResult = SectionEqualVolumeFixture.SetDense(dense, edit);
            SectionWriteResult adaptiveResult = SectionEqualVolumeFixture.SetGlobal(adaptive, _layout, edit);
            if (denseResult != adaptiveResult)
            {
                throw new InvalidOperationException($"Adaptive/dense edit result mismatch at global index {edit.GlobalIndex}.");
            }
        }

        SectionBenchmarkSupport.ValidateEqualWorld(adaptive, _layout, dense);
    }

    private static SectionWriteResult SetDenseUnchecked(Span<BlockStateId> dense, SectionEdit edit)
    {
        if (dense[edit.GlobalIndex].Equals(edit.State))
        {
            return SectionWriteResult.Unchanged;
        }

        dense[edit.GlobalIndex] = edit.State;
        return SectionWriteResult.Changed;
    }
}

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId, "EqualVolumeGrowth")]
public class EqualVolumePaletteGrowthBenchmarks
{
    private SectionEqualVolumeLayout _layout;
    private MutableSectionBlockStates[] _candidate = null!;
    private SectionEdit[] _growthTrace = null!;

    [Params("OneSide32", "EightSide16")]
    public string Layout { get; set; } = "OneSide32";

    [GlobalSetup]
    public void GlobalSetup()
    {
        SectionBenchmarkSupport.EmitObservationManifestOnce(SectionCandidateFixture.DefaultSeed, "Release");
        _layout = SectionBenchmarkSupport.ParseLayout(Layout);
        BlockStateId[] canonical = new BlockStateId[SectionEqualVolumeFixture.CubeVolume];
        MutableSectionBlockStates[] validationCandidate = SectionEqualVolumeFixture.CreateSections(_layout, canonical);
        SectionEqualVolumeFixture.ValidateSections(validationCandidate, _layout);
        SectionBenchmarkSupport.ValidateEqualWorld(validationCandidate, _layout, canonical);
        _growthTrace = new SectionEdit[257];
        for (int index = 0; index < _growthTrace.Length; index++)
        {
            _growthTrace[index] = new SectionEdit(
                index * 127 % SectionEqualVolumeFixture.CubeVolume,
                new BlockStateId(checked((uint)index + 1U)),
                SectionEditIntent.NewStateChange);
        }
    }

    [IterationSetup]
    public void Setup()
    {
        _candidate = SectionEqualVolumeFixture.CreateSections(
            _layout,
            new BlockStateId[SectionEqualVolumeFixture.CubeVolume]);
        SectionEqualVolumeFixture.ValidateSections(_candidate, _layout);
    }

    [Benchmark]
    public ulong PaletteGrowthAndRepack()
    {
        ulong checksum = 0;
        foreach (SectionEdit edit in _growthTrace)
        {
            SectionWriteResult result = SectionEqualVolumeFixture.SetGlobalUnchecked(_candidate, _layout, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit, result);
        }

        return checksum;
    }
}
