using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

internal sealed class MutableSectionBlockStates : IReadOnlySectionBlockStates
{
    private BlockStateStorage _storage;

    internal MutableSectionBlockStates(
        SectionGeometry geometry,
        WorldStateId initialState,
        SectionRevision revision)
    {
        Geometry = ValidateGeometry(geometry);
        Count = checked(Geometry.Side.Value * Geometry.Side.Value * Geometry.Side.Value);
        Revision = revision;
        _storage = new UniformBlockStateStorage(Count, initialState);
    }

    public SectionGeometry Geometry { get; }

    public SectionRevision Revision { get; private set; }

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

    internal SectionWriteResult TrySet(LocalBlock local, WorldStateId state)
    {
        int index = Geometry.GetLinearIndex(local);
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
        WorldStateId[] states = new WorldStateId[Count];
        _storage.CopyTo(states);
        return SectionBlockStateSnapshot.Create(Geometry, Revision, states);
    }

    private static SectionGeometry ValidateGeometry(SectionGeometry geometry)
    {
        return new SectionGeometry(geometry.Side);
    }
}
