using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;

namespace VibeCraft.WorldModel.Sections;

internal enum SectionEqualVolumeLayout : byte
{
    OneSide32 = 0,
    EightSide16 = 1,
}

internal enum SectionEditTraceKind : byte
{
    InteriorClusters = 0,
    BoundaryClusters = 1,
}

internal enum SectionEditIntent : byte
{
    NoOp = 0,
    ExistingStateChange = 1,
    NewStateChange = 2,
}

internal readonly record struct SectionEdit(
    int GlobalIndex,
    BlockStateId State,
    SectionEditIntent Intent);

internal readonly record struct SectionCellAddress(
    int SectionIndex,
    LocalIndex LocalIndex);

internal readonly record struct SectionAddressedEdit(
    SectionEdit Edit,
    SectionCellAddress Address);

/// <summary>
/// Builds equal-world-volume candidates and deterministic global edit traces for the ephemeral G1 experiment.
/// </summary>
internal static class SectionEqualVolumeFixture
{
    private static readonly LocalIndex[] Side16LocalIndexTrace = CreateSide16LocalIndexTrace();

    internal const int CubeSide = 32;
    internal const int CubeVolume = CubeSide * CubeSide * CubeSide;
    internal const int DefaultClusterCount = 256;
    internal const int EditsPerCluster = 4 * 4 * 4;

    internal static BlockStateId[] CreateCanonicalCube(
        SectionFixtureKind fixture,
        ulong seed = SectionCandidateFixture.DefaultSeed)
    {
        return SectionCandidateFixture.CreateStates(SectionGeometry.Side32, fixture, seed);
    }

    internal static MutableSectionBlockStates[] CreateSections(
        SectionEqualVolumeLayout layout,
        ReadOnlySpan<BlockStateId> canonicalCube)
    {
        ValidateCanonicalCube(canonicalCube);
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return [SectionCandidateFixture.CreateSection(SectionGeometry.Side32, canonicalCube)];
        }

        if (layout != SectionEqualVolumeLayout.EightSide16)
        {
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "The equal-volume layout is undefined.");
        }

        MutableSectionBlockStates[] sections = new MutableSectionBlockStates[8];
        for (int sectionY = 0; sectionY < 2; sectionY++)
        {
            for (int sectionZ = 0; sectionZ < 2; sectionZ++)
            {
                for (int sectionX = 0; sectionX < 2; sectionX++)
                {
                    int sectionIndex = GetSectionIndex(sectionX, sectionY, sectionZ);
                    BlockStateId[] octant = SectionCandidateFixture.ExtractSide16(canonicalCube, sectionX, sectionY, sectionZ);
                    sections[sectionIndex] = SectionCandidateFixture.CreateSection(SectionGeometry.Side16, octant);
                }
            }
        }

        return sections;
    }

    internal static BlockStateId[][] CreateDenseSections(
        SectionEqualVolumeLayout layout,
        ReadOnlySpan<BlockStateId> canonicalCube)
    {
        ValidateCanonicalCube(canonicalCube);
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return [canonicalCube.ToArray()];
        }

        if (layout != SectionEqualVolumeLayout.EightSide16)
        {
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "The equal-volume layout is undefined.");
        }

        BlockStateId[][] sections = new BlockStateId[8][];
        for (int sectionY = 0; sectionY < 2; sectionY++)
        {
            for (int sectionZ = 0; sectionZ < 2; sectionZ++)
            {
                for (int sectionX = 0; sectionX < 2; sectionX++)
                {
                    int sectionIndex = GetSectionIndex(sectionX, sectionY, sectionZ);
                    sections[sectionIndex] = SectionCandidateFixture.ExtractSide16(canonicalCube, sectionX, sectionY, sectionZ);
                }
            }
        }

        return sections;
    }

    internal static SectionCellAddress[] CreateAddressTrace(
        SectionEqualVolumeLayout layout,
        ReadOnlySpan<int> globalIndices)
    {
        ValidateLayout(layout);
        SectionCellAddress[] addresses = new SectionCellAddress[globalIndices.Length];
        for (int index = 0; index < globalIndices.Length; index++)
        {
            ValidateGlobalIndex(globalIndices[index]);
            addresses[index] = GetCellAddressUnchecked(layout, globalIndices[index]);
        }

        return addresses;
    }

    internal static SectionAddressedEdit[] CreateAddressedEditTrace(
        SectionEqualVolumeLayout layout,
        ReadOnlySpan<SectionEdit> edits)
    {
        ValidateLayout(layout);
        SectionAddressedEdit[] addressed = new SectionAddressedEdit[edits.Length];
        for (int index = 0; index < edits.Length; index++)
        {
            ValidateGlobalIndex(edits[index].GlobalIndex);
            addressed[index] = new SectionAddressedEdit(edits[index], GetCellAddressUnchecked(layout, edits[index].GlobalIndex));
        }

        return addressed;
    }

    internal static BlockStateId GetAddressedUnchecked(
        MutableSectionBlockStates[] sections,
        SectionCellAddress address)
    {
        return sections[address.SectionIndex].Get(address.LocalIndex);
    }

    internal static BlockStateId GetDenseAddressedUnchecked(
        BlockStateId[][] sections,
        SectionCellAddress address)
    {
        return sections[address.SectionIndex][address.LocalIndex.Value];
    }

    internal static SectionWriteResult SetAddressedUnchecked(
        MutableSectionBlockStates[] sections,
        SectionAddressedEdit edit)
    {
        return sections[edit.Address.SectionIndex].TrySet(edit.Address.LocalIndex, edit.Edit.State);
    }

    internal static SectionWriteResult SetDenseAddressedUnchecked(
        BlockStateId[][] sections,
        SectionAddressedEdit edit)
    {
        ref BlockStateId current = ref sections[edit.Address.SectionIndex][edit.Address.LocalIndex.Value];
        if (current.Equals(edit.Edit.State))
        {
            return SectionWriteResult.Unchanged;
        }

        current = edit.Edit.State;
        return SectionWriteResult.Changed;
    }

    internal static void CopyDenseToCanonical(
        BlockStateId[][] sections,
        SectionEqualVolumeLayout layout,
        Span<BlockStateId> destination)
    {
        ValidateDenseSections(sections, layout);
        if (destination.Length < CubeVolume)
        {
            throw new ArgumentException($"The canonical projection requires at least {CubeVolume} states.", nameof(destination));
        }

        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            sections[0].CopyTo(destination);
            return;
        }

        for (int globalIndex = 0; globalIndex < CubeVolume; globalIndex++)
        {
            SectionCellAddress address = GetCellAddressUnchecked(layout, globalIndex);
            destination[globalIndex] = GetDenseAddressedUnchecked(sections, address);
        }
    }

    internal static BlockStateId GetGlobal(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        int globalIndex)
    {
        ValidateSections(sections, layout);
        ValidateGlobalIndex(globalIndex);
        return GetGlobalUnchecked(sections, layout, globalIndex);
    }

    /// <summary>
    /// Reads a previously validated equal-volume layout without repeating fixture validation in a measured hot path.
    /// </summary>
    internal static BlockStateId GetGlobalUnchecked(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        int globalIndex)
    {
        DecomposeGlobalIndex(globalIndex, out int x, out int y, out int z);
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return sections[0].Get(new LocalBlock(x, y, z));
        }

        int sectionIndex = GetSectionIndex(x >> 4, y >> 4, z >> 4);
        return sections[sectionIndex].Get(new LocalBlock(x & 15, y & 15, z & 15));
    }

    internal static SectionWriteResult SetGlobal(
        MutableSectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        SectionEdit edit)
    {
        ValidateSections(sections, layout);
        ValidateGlobalIndex(edit.GlobalIndex);
        return SetGlobalUnchecked(sections, layout, edit);
    }

    /// <summary>
    /// Mutates a previously validated equal-volume layout without repeating fixture validation in a measured hot path.
    /// </summary>
    internal static SectionWriteResult SetGlobalUnchecked(
        MutableSectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        SectionEdit edit)
    {
        DecomposeGlobalIndex(edit.GlobalIndex, out int x, out int y, out int z);
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return sections[0].TrySet(new LocalBlock(x, y, z), edit.State);
        }

        int sectionIndex = GetSectionIndex(x >> 4, y >> 4, z >> 4);
        return sections[sectionIndex].TrySet(new LocalBlock(x & 15, y & 15, z & 15), edit.State);
    }

    internal static SectionWriteResult SetDense(Span<BlockStateId> dense, SectionEdit edit)
    {
        if (dense.Length != CubeVolume)
        {
            throw new ArgumentException($"The dense equal-volume baseline requires exactly {CubeVolume} states.", nameof(dense));
        }

        ValidateGlobalIndex(edit.GlobalIndex);
        if (dense[edit.GlobalIndex].Equals(edit.State))
        {
            return SectionWriteResult.Unchanged;
        }

        dense[edit.GlobalIndex] = edit.State;
        return SectionWriteResult.Changed;
    }

    internal static void CopyToCanonical(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        Span<BlockStateId> destination,
        BlockStateId[][] side16Scratch)
    {
        ValidateSections(sections, layout);
        if (destination.Length < CubeVolume)
        {
            throw new ArgumentException($"The canonical projection requires at least {CubeVolume} states.", nameof(destination));
        }

        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            sections[0].CopyTo(destination);
            return;
        }

        ValidateSide16Scratch(side16Scratch);
        CopyToCanonicalUnchecked(sections, layout, destination, side16Scratch);
    }

    /// <summary>
    /// Copies a previously validated equal-volume layout using caller-owned, distinct scratch arrays.
    /// </summary>
    internal static void CopyToCanonicalUnchecked(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        Span<BlockStateId> destination,
        BlockStateId[][] side16Scratch)
    {
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            sections[0].CopyTo(destination);
            return;
        }

        for (int sectionIndex = 0; sectionIndex < 8; sectionIndex++)
        {
            sections[sectionIndex].CopyTo(side16Scratch[sectionIndex]);
        }

        for (int globalIndex = 0; globalIndex < CubeVolume; globalIndex++)
        {
            DecomposeGlobalIndex(globalIndex, out int x, out int y, out int z);
            int sectionIndex = GetSectionIndex(x >> 4, y >> 4, z >> 4);
            LocalIndex localIndex = Side16LocalIndexTrace[globalIndex];
            destination[globalIndex] = side16Scratch[sectionIndex][localIndex.Value];
        }
    }

    private static LocalIndex[] CreateSide16LocalIndexTrace()
    {
        LocalIndex[] trace = new LocalIndex[CubeVolume];
        for (int globalIndex = 0; globalIndex < trace.Length; globalIndex++)
        {
            DecomposeGlobalIndex(globalIndex, out int x, out int y, out int z);
            trace[globalIndex] = SectionGeometry.Side16.GetLocalIndex(new LocalBlock(x & 15, y & 15, z & 15));
        }

        return trace;
    }

    private static void ValidateSide16Scratch(BlockStateId[][] side16Scratch)
    {
        ArgumentNullException.ThrowIfNull(side16Scratch);
        if (side16Scratch.Length < 8)
        {
            throw new ArgumentException("Eight side-16 scratch buffers of at least 4096 states are required.", nameof(side16Scratch));
        }

        for (int sectionIndex = 0; sectionIndex < 8; sectionIndex++)
        {
            if (side16Scratch[sectionIndex] is null || side16Scratch[sectionIndex].Length < 16 * 16 * 16)
            {
                throw new ArgumentException("Eight side-16 scratch buffers of at least 4096 states are required.", nameof(side16Scratch));
            }

            for (int previousIndex = 0; previousIndex < sectionIndex; previousIndex++)
            {
                if (ReferenceEquals(side16Scratch[sectionIndex], side16Scratch[previousIndex]))
                {
                    throw new ArgumentException("The eight side-16 scratch buffers must be distinct arrays.", nameof(side16Scratch));
                }
            }
        }
    }

    internal static SectionEdit[] CreateEditTrace(
        ReadOnlySpan<BlockStateId> canonicalCube,
        SectionEditTraceKind traceKind,
        ulong seed = SectionCandidateFixture.DefaultSeed,
        int clusterCount = DefaultClusterCount)
    {
        ValidateCanonicalCube(canonicalCube);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clusterCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clusterCount, 4096);
        if (traceKind is not (SectionEditTraceKind.InteriorClusters or SectionEditTraceKind.BoundaryClusters))
        {
            throw new ArgumentOutOfRangeException(nameof(traceKind), traceKind, "The edit trace kind is undefined.");
        }

        BlockStateId[] working = canonicalCube.ToArray();
        List<BlockStateId> existingStates = [.. canonicalCube.ToArray().Distinct().OrderBy(state => state.Value)];
        HashSet<BlockStateId> knownStates = [.. existingStates];
        SectionEdit[] trace = new SectionEdit[checked(clusterCount * EditsPerCluster)];
        ulong random = seed ^ ((ulong)traceKind * 0xA0761D6478BD642FUL);
        int traceIndex = 0;
        for (int cluster = 0; cluster < clusterCount; cluster++)
        {
            GetClusterOrigin(traceKind, cluster, ref random, out int originX, out int originY, out int originZ);
            for (int localY = 0; localY < 4; localY++)
            {
                for (int localZ = 0; localZ < 4; localZ++)
                {
                    for (int localX = 0; localX < 4; localX++)
                    {
                        int x = originX + localX;
                        int y = originY + localY;
                        int z = originZ + localZ;
                        int globalIndex = x + (CubeSide * (z + (CubeSide * y)));
                        SectionEditIntent requestedIntent = (traceIndex % 4) switch
                        {
                            0 or 3 => SectionEditIntent.NoOp,
                            1 => SectionEditIntent.ExistingStateChange,
                            _ => SectionEditIntent.NewStateChange,
                        };
                        BlockStateId state = SelectEditState(
                            working[globalIndex],
                            existingStates,
                            requestedIntent,
                            traceIndex,
                            out SectionEditIntent actualIntent);
                        trace[traceIndex] = new SectionEdit(globalIndex, state, actualIntent);
                        if (actualIntent != SectionEditIntent.NoOp)
                        {
                            working[globalIndex] = state;
                            if (knownStates.Add(state))
                            {
                                existingStates.Add(state);
                            }
                        }

                        traceIndex++;
                    }
                }
            }
        }

        return trace;
    }

    internal static int GetSectionIndexForGlobal(
        SectionEqualVolumeLayout layout,
        int globalIndex)
    {
        ValidateGlobalIndex(globalIndex);
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return 0;
        }

        if (layout != SectionEqualVolumeLayout.EightSide16)
        {
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "The equal-volume layout is undefined.");
        }

        DecomposeGlobalIndex(globalIndex, out int x, out int y, out int z);
        return GetSectionIndex(x >> 4, y >> 4, z >> 4);
    }

    internal static void GetSectionCoordinates(
        SectionEqualVolumeLayout layout,
        int sectionIndex,
        out int sectionX,
        out int sectionY,
        out int sectionZ)
    {
        int sectionCount = layout == SectionEqualVolumeLayout.OneSide32 ? 1 : layout == SectionEqualVolumeLayout.EightSide16 ? 8 : 0;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)sectionIndex, (uint)sectionCount, nameof(sectionIndex));
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            sectionX = 0;
            sectionY = 0;
            sectionZ = 0;
            return;
        }

        sectionX = sectionIndex & 1;
        sectionZ = (sectionIndex >> 1) & 1;
        sectionY = (sectionIndex >> 2) & 1;
    }

    private static BlockStateId SelectEditState(
        BlockStateId current,
        List<BlockStateId> existingStates,
        SectionEditIntent requestedIntent,
        int traceIndex,
        out SectionEditIntent actualIntent)
    {
        if (requestedIntent == SectionEditIntent.NoOp)
        {
            actualIntent = requestedIntent;
            return current;
        }

        if (requestedIntent == SectionEditIntent.ExistingStateChange && existingStates.Count > 1)
        {
            int start = traceIndex % existingStates.Count;
            for (int offset = 0; offset < existingStates.Count; offset++)
            {
                BlockStateId candidate = existingStates[(start + offset) % existingStates.Count];
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

    private static void GetClusterOrigin(
        SectionEditTraceKind traceKind,
        int cluster,
        ref ulong random,
        out int x,
        out int y,
        out int z)
    {
        if (traceKind == SectionEditTraceKind.InteriorClusters)
        {
            x = GetInteriorOrigin(cluster, ref random);
            y = GetInteriorOrigin(cluster >> 1, ref random);
            z = GetInteriorOrigin(cluster >> 2, ref random);
            return;
        }

        int[] origins = [14, 15];
        x = origins[cluster & 1];
        y = origins[(cluster >> 1) & 1];
        z = origins[(cluster >> 2) & 1];
        _ = Next(ref random);
    }

    private static int GetInteriorOrigin(int selector, ref ulong random)
    {
        int octantOrigin = (selector & 1) * 16;
        return octantOrigin + 3 + checked((int)(Next(ref random) % 7UL));
    }

    private static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static void ValidateCanonicalCube(ReadOnlySpan<BlockStateId> canonicalCube)
    {
        if (canonicalCube.Length != CubeVolume)
        {
            throw new ArgumentException($"An equal-volume canonical cube requires exactly {CubeVolume} states.", nameof(canonicalCube));
        }
    }

    internal static void ValidateSections(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout)
    {
        ArgumentNullException.ThrowIfNull(sections);
        int expectedCount = layout switch
        {
            SectionEqualVolumeLayout.OneSide32 => 1,
            SectionEqualVolumeLayout.EightSide16 => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "The equal-volume layout is undefined."),
        };
        if (sections.Length != expectedCount)
        {
            throw new ArgumentException($"The {layout} layout requires exactly {expectedCount} initialized sections.", nameof(sections));
        }

        SectionSide expectedSide = layout == SectionEqualVolumeLayout.OneSide32
            ? SectionSide.ThirtyTwo
            : SectionSide.Sixteen;
        int expectedVolume = checked(expectedSide.Value * expectedSide.Value * expectedSide.Value);
        for (int index = 0; index < sections.Length; index++)
        {
            IReadOnlySectionBlockStates section = sections[index]
                ?? throw new ArgumentException($"The {layout} layout requires exactly {expectedCount} initialized sections.", nameof(sections));

            if (section.Geometry.Side != expectedSide || section.Count != expectedVolume)
            {
                throw new ArgumentException(
                    $"The {layout} layout requires every section to use side {expectedSide.Value} and volume {expectedVolume}.",
                    nameof(sections));
            }
        }
    }

    private static void ValidateGlobalIndex(int globalIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)globalIndex, (uint)CubeVolume, nameof(globalIndex));
    }

    private static SectionCellAddress GetCellAddressUnchecked(
        SectionEqualVolumeLayout layout,
        int globalIndex)
    {
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return new SectionCellAddress(0, new LocalIndex(globalIndex));
        }

        DecomposeGlobalIndex(globalIndex, out int x, out int y, out int z);
        return new SectionCellAddress(
            GetSectionIndex(x >> 4, y >> 4, z >> 4),
            Side16LocalIndexTrace[globalIndex]);
    }

    private static void ValidateLayout(SectionEqualVolumeLayout layout)
    {
        if (layout is not (SectionEqualVolumeLayout.OneSide32 or SectionEqualVolumeLayout.EightSide16))
        {
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "The equal-volume layout is undefined.");
        }
    }

    private static void ValidateDenseSections(BlockStateId[][] sections, SectionEqualVolumeLayout layout)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ValidateLayout(layout);
        int expectedCount = layout == SectionEqualVolumeLayout.OneSide32 ? 1 : 8;
        int expectedVolume = layout == SectionEqualVolumeLayout.OneSide32 ? CubeVolume : 16 * 16 * 16;
        if (sections.Length != expectedCount || sections.Any(section => section is null || section.Length != expectedVolume))
        {
            throw new ArgumentException(
                $"The dense {layout} layout requires exactly {expectedCount} arrays of {expectedVolume} states.",
                nameof(sections));
        }
    }

    private static void DecomposeGlobalIndex(int globalIndex, out int x, out int y, out int z)
    {
        x = globalIndex % CubeSide;
        z = globalIndex / CubeSide % CubeSide;
        y = globalIndex / (CubeSide * CubeSide);
    }

    private static int GetSectionIndex(int sectionX, int sectionY, int sectionZ)
    {
        return sectionX + (2 * (sectionZ + (2 * sectionY)));
    }
}
