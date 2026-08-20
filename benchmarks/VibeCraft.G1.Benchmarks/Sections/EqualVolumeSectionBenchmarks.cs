using BenchmarkDotNet.Attributes;
using VibeCraft.Content;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId)]
public class EqualVolumeSectionReadBenchmarks
{
    private BlockStateId[] _canonical = null!;
    private BlockStateId[][] _denseSections = null!;
    private SectionEqualVolumeLayout _layout;
    private MutableSectionBlockStates[] _sections = null!;
    private BlockStateId[] _projection = null!;
    private BlockStateId[][] _side16Scratch = null!;
    private SectionCellAddress[] _randomTrace = null!;
    private SectionCellAddress[] _linearTrace = null!;

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
        _denseSections = SectionEqualVolumeFixture.CreateDenseSections(_layout, _canonical);
        SectionEqualVolumeFixture.ValidateSections(_sections, _layout);
        SectionBenchmarkSupport.ValidateEqualWorld(_sections, _layout, _canonical);
        _projection = new BlockStateId[_canonical.Length];
        _side16Scratch = SectionBenchmarkSupport.CreateSide16Scratch();
        SectionEqualVolumeFixture.CopyToCanonical(_sections, _layout, _projection, _side16Scratch);
        int[] randomGlobalTrace = SectionBenchmarkSupport.CreateRandomTrace(
            SectionBenchmarkSupport.RandomTraceLength,
            _canonical.Length,
            seed ^ 0xE7037ED1A0B428DBUL);
        _randomTrace = SectionEqualVolumeFixture.CreateAddressTrace(_layout, randomGlobalTrace);
        int[] linearGlobalTrace = new int[_canonical.Length];
        for (int index = 0; index < linearGlobalTrace.Length; index++)
        {
            linearGlobalTrace[index] = index;
        }

        _linearTrace = SectionEqualVolumeFixture.CreateAddressTrace(_layout, linearGlobalTrace);
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeRandomReads")]
    public ulong AdaptiveRandomReads()
    {
        ulong checksum = 0;
        foreach (SectionCellAddress address in _randomTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetAddressedUnchecked(_sections, address).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeRandomReads")]
    public ulong DenseRandomReads()
    {
        ulong checksum = 0;
        foreach (SectionCellAddress address in _randomTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetDenseAddressedUnchecked(_denseSections, address).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeLinearReads")]
    public ulong AdaptiveLinearReads()
    {
        ulong checksum = 0;
        foreach (SectionCellAddress address in _linearTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetAddressedUnchecked(_sections, address).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeLinearReads")]
    public ulong DenseLinearReads()
    {
        ulong checksum = 0;
        foreach (SectionCellAddress address in _linearTrace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetDenseAddressedUnchecked(_denseSections, address).Value);
        }

        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("EqualVolumeSnapshots")]
    public ulong CaptureSnapshots()
    {
        ulong checksum = 0;
        foreach (MutableSectionBlockStates candidate in _sections)
        {
            SectionBlockStateSnapshot snapshot = candidate.CaptureSnapshot();
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
    private BlockStateId[][] _denseCandidate = null!;
    private SectionEqualVolumeLayout _layout;
    private MutableSectionBlockStates[] _adaptiveCandidate = null!;
    private SectionEdit[] _trace = null!;
    private SectionAddressedEdit[] _addressedTrace = null!;

    [Params("OneSide32", "EightSide16")]
    public string Layout { get; set; } = "OneSide32";

    [Params("UniformAir", "UniformStone", "Layered", "Mixed", "HighEntropy")]
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
        _addressedTrace = SectionEqualVolumeFixture.CreateAddressedEditTrace(_layout, _trace);
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
        _denseCandidate = SectionEqualVolumeFixture.CreateDenseSections(_layout, _canonical);
    }

    [Benchmark]
    public ulong AdaptiveClusteredEdits()
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (SectionAddressedEdit edit in _addressedTrace)
        {
            SectionWriteResult result = SectionEqualVolumeFixture.SetAddressedUnchecked(_adaptiveCandidate, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit.Edit, result);
        }

        return checksum;
    }

    [Benchmark(Baseline = true)]
    public ulong DenseClusteredEdits()
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (SectionAddressedEdit edit in _addressedTrace)
        {
            SectionWriteResult result = SectionEqualVolumeFixture.SetDenseAddressedUnchecked(_denseCandidate, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit.Edit, result);
        }

        return checksum;
    }

    private void ValidateTraceEquivalence()
    {
        BlockStateId[][] dense = SectionEqualVolumeFixture.CreateDenseSections(_layout, _canonical);
        MutableSectionBlockStates[] adaptive = SectionEqualVolumeFixture.CreateSections(_layout, _canonical);
        foreach (SectionAddressedEdit edit in _addressedTrace)
        {
            SectionWriteResult denseResult = SectionEqualVolumeFixture.SetDenseAddressedUnchecked(dense, edit);
            SectionWriteResult adaptiveResult = SectionEqualVolumeFixture.SetAddressedUnchecked(adaptive, edit);
            if (denseResult != adaptiveResult)
            {
                throw new InvalidOperationException($"Adaptive/dense edit result mismatch at global index {edit.Edit.GlobalIndex}.");
            }
        }

        BlockStateId[] denseCanonical = new BlockStateId[SectionEqualVolumeFixture.CubeVolume];
        SectionEqualVolumeFixture.CopyDenseToCanonical(dense, _layout, denseCanonical);
        SectionBenchmarkSupport.ValidateEqualWorld(adaptive, _layout, denseCanonical);
    }
}

[MemoryDiagnoser]
[BenchmarkCategory(SectionCandidateFixture.FixtureId, "EqualVolumeGrowth")]
public class EqualVolumePaletteGrowthBenchmarks
{
    private SectionEqualVolumeLayout _layout;
    private MutableSectionBlockStates[] _candidate = null!;
    private SectionEdit[] _growthTrace = null!;
    private SectionAddressedEdit[] _addressedGrowthTrace = null!;

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

        _addressedGrowthTrace = SectionEqualVolumeFixture.CreateAddressedEditTrace(_layout, _growthTrace);
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
        foreach (SectionAddressedEdit edit in _addressedGrowthTrace)
        {
            SectionWriteResult result = SectionEqualVolumeFixture.SetAddressedUnchecked(_candidate, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit.Edit, result);
        }

        return checksum;
    }
}
