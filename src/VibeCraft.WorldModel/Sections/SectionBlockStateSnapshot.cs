using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

internal sealed class SectionBlockStateSnapshot : IReadOnlySectionBlockStates
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
        int expectedCount = checked(Geometry.Side.Value * Geometry.Side.Value * Geometry.Side.Value);
        if (Count != expectedCount)
        {
            throw new ArgumentException($"Snapshot storage requires exactly {expectedCount} entries for this geometry.", nameof(storage));
        }
    }

    internal static SectionBlockStateSnapshot Create(
        SectionGeometry geometry,
        SectionRevision revision,
        ReadOnlySpan<WorldStateId> semanticStates)
    {
        SectionGeometry validatedGeometry = new(geometry.Side);
        int side = validatedGeometry.Side.Value;
        int expectedCount = checked(side * side * side);
        if (semanticStates.Length != expectedCount)
        {
            throw new ArgumentException($"A snapshot requires exactly {expectedCount} semantic states for this geometry.", nameof(semanticStates));
        }

        HashSet<WorldStateId> distinct = [.. semanticStates];
        BlockStateStorage storage;
        if (distinct.Count == 1)
        {
            storage = new UniformBlockStateStorage(expectedCount, semanticStates[0]);
        }
        else if (distinct.Count <= 256)
        {
            WorldStateId[] sortedPalette = [.. distinct.OrderBy(state => state.Value)];
            storage = PalettedBlockStateStorage.FromCanonical(semanticStates, sortedPalette);
        }
        else
        {
            storage = new DirectBlockStateStorage(semanticStates.ToArray());
        }

        return new SectionBlockStateSnapshot(validatedGeometry, revision, storage);
    }

    public SectionGeometry Geometry { get; }

    public SectionRevision Revision { get; }

    public int Count { get; }

    public SectionBlockStorageKind StorageKind => _storage.Kind;

    public WorldStateId Get(LocalBlock local)
    {
        return _storage.Get(Geometry.GetLinearIndex(local));
    }

    public void CopyTo(Span<WorldStateId> destination)
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
