using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

/// <summary>
/// Exposes an immutable semantic view of the block states captured for one section revision.
/// </summary>
/// <remarks>
/// This consumer contract exposes no container, palette, buffer, persistence, or wire representation.
/// Its compatibility status remains provisional until a written G1 greenlight freezes the handoff.
/// Implementations must remain stable for their lifetime and support concurrent reads without locks.
/// </remarks>
public interface ISectionBlockStateSnapshot
{
    /// <summary>Gets the explicit geometry that bounds every local index in this snapshot.</summary>
    SectionGeometry Geometry { get; }

    /// <summary>Gets the exact section revision captured with the semantic block-state values.</summary>
    SectionRevision Revision { get; }

    /// <summary>Gets the semantic block-state identity at one valid section-local index.</summary>
    /// <param name="index">An index in the range zero through <c>Geometry.Volume - 1</c>.</param>
    /// <returns>The captured world-local block-state identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside this snapshot's geometry.</exception>
    BlockStateId GetBlockState(LocalIndex index);
}
