using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

internal sealed class MutableSectionBlockStates : IReadOnlySectionBlockStates
{
    private BlockStateStorage _storage;

    internal MutableSectionBlockStates(
        SectionGeometry geometry,
        BlockStateId initialState,
        SectionRevision revision)
    {
        Geometry = ValidateGeometry(geometry);
        Count = Geometry.Volume;
        Revision = revision;
        _storage = new UniformBlockStateStorage(Count, initialState);
    }

    public SectionGeometry Geometry { get; }

    public SectionRevision Revision { get; private set; }

    public int Count { get; }

    public SectionBlockStorageKind StorageKind => _storage.Kind;

    public BlockStateId Get(LocalIndex index)
    {
        return _storage.Get(index);
    }

    public BlockStateId Get(LocalBlock local)
    {
        return Get(Geometry.GetLocalIndex(local));
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

    internal SectionWriteResult TrySet(LocalBlock local, BlockStateId state)
    {
        return TrySet(Geometry.GetLocalIndex(local), state);
    }

    internal SectionWriteResult TrySet(LocalIndex index, BlockStateId state)
    {
        if (_storage.Get(index).Equals(state))
        {
            return SectionWriteResult.Unchanged;
        }

        if (Revision.TryNext(out SectionRevision next) != SectionRevisionAdvanceResult.Advanced)
        {
            return SectionWriteResult.RevisionExhausted;
        }

        BlockStateStorage updated = _storage.Set(index, state);
        _storage = updated;
        Revision = next;
        return SectionWriteResult.Changed;
    }

    internal SectionBlockStateSnapshot CaptureSnapshot()
    {
        if (_storage is UniformBlockStateStorage uniform)
        {
            return SectionBlockStateSnapshot.CreateFromUniformStorage(Geometry, Revision, uniform);
        }

        if (_storage is DirectBlockStateStorage direct && direct.HasMoreThanSnapshotPaletteLimit())
        {
            return SectionBlockStateSnapshot.CreateFromDirectStorage(Geometry, Revision, direct);
        }

        BlockStateId[] states = new BlockStateId[Count];
        _storage.CopyTo(states);
        return SectionBlockStateSnapshot.Create(Geometry, Revision, states);
    }

    private static SectionGeometry ValidateGeometry(SectionGeometry geometry)
    {
        return new SectionGeometry(geometry.Side);
    }
}
