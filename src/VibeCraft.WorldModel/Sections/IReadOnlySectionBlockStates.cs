using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

internal interface IReadOnlySectionBlockStates
{
    SectionGeometry Geometry { get; }

    SectionRevision Revision { get; }

    int Count { get; }

    SectionBlockStorageKind StorageKind { get; }

    BlockStateId Get(LocalIndex index);

    BlockStateId Get(LocalBlock local);

    void CopyTo(Span<BlockStateId> destination);

    SectionStorageMetrics GetStorageMetrics();
}
