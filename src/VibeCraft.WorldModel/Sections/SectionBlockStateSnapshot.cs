using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

internal sealed class SectionBlockStateSnapshot : IReadOnlySectionBlockStates, ISectionBlockStateSnapshot
{
    private readonly BlockStateStorage _storage;

    private SectionBlockStateSnapshot(
        SectionGeometry geometry,
        SectionRevision revision,
        BlockStateStorage storage)
    {
        Geometry = new SectionGeometry(geometry.Side);
        Revision = revision;
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        Count = storage.Count;
        int expectedCount = Geometry.Volume;
        if (Count != expectedCount)
        {
            throw new ArgumentException($"Snapshot storage requires exactly {expectedCount} entries for this geometry.", nameof(storage));
        }
    }

    internal static SectionBlockStateSnapshot Create(
        SectionGeometry geometry,
        SectionRevision revision,
        ReadOnlySpan<BlockStateId> semanticStates)
    {
        SectionGeometry validatedGeometry = new(geometry.Side);
        int expectedCount = validatedGeometry.Volume;
        if (semanticStates.Length != expectedCount)
        {
            throw new ArgumentException($"A snapshot requires exactly {expectedCount} semantic states for this geometry.", nameof(semanticStates));
        }

        HashSet<BlockStateId> distinct = [.. semanticStates];
        BlockStateStorage storage;
        if (distinct.Count == 1)
        {
            storage = new UniformBlockStateStorage(expectedCount, semanticStates[0]);
        }
        else if (distinct.Count <= 256)
        {
            BlockStateId[] sortedPalette = [.. distinct.OrderBy(state => state.Value)];
            storage = PalettedBlockStateStorage.FromCanonical(semanticStates, sortedPalette);
        }
        else
        {
            storage = new DirectBlockStateStorage(semanticStates.ToArray());
        }

        return new SectionBlockStateSnapshot(validatedGeometry, revision, storage);
    }

    internal static SectionBlockStateSnapshot CreateFromUniformStorage(
        SectionGeometry geometry,
        SectionRevision revision,
        UniformBlockStateStorage uniformStorage)
    {
        return new SectionBlockStateSnapshot(geometry, revision, uniformStorage);
    }

    internal static SectionBlockStateSnapshot CreateFromDirectStorage(
        SectionGeometry geometry,
        SectionRevision revision,
        DirectBlockStateStorage directStorage)
    {
        return new SectionBlockStateSnapshot(geometry, revision, directStorage.CloneForSnapshot());
    }

    public SectionGeometry Geometry { get; }

    public SectionRevision Revision { get; }

    public int Count { get; }

    public SectionBlockStorageKind StorageKind => _storage.Kind;

    public BlockStateId Get(LocalBlock local)
    {
        return Get(Geometry.GetLocalIndex(local));
    }

    public BlockStateId Get(LocalIndex index)
    {
        return GetBlockState(index);
    }

    public BlockStateId GetBlockState(LocalIndex index)
    {
        return _storage.Get(index);
    }

    public void CopyTo(Span<BlockStateId> destination)
    {
        if (destination.Length < Count)
        {
            throw new ArgumentException($"The destination requires at least {Count} entries.", nameof(destination));
        }

        _storage.CopyTo(destination);
    }

    public SectionStorageMetrics GetStorageMetrics()
    {
        return _storage.GetMetrics();
    }
}
