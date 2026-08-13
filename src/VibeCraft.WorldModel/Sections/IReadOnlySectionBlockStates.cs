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

    WorldStateId Get(LocalBlock local);

    void CopyTo(Span<WorldStateId> destination);

    SectionStorageMetrics GetStorageMetrics();
}
