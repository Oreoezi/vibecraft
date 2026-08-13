using System.Collections.Immutable;
using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Defines one immutable canonical section-state record for a G1 logical fixture.
/// </summary>
/// <remarks>
/// This is semantic fixture data, not a persistence, database, migration, wire, or user-world
/// format. In particular, it intentionally has no storage-kind or revision field.
/// </remarks>
public sealed class LogicalSectionRecord
{
    private LogicalSectionRecord(
        LogicalRecordKey key,
        SectionGeometry geometry,
        BlockCoord origin,
        BlockCoord endInclusive,
        ImmutableArray<WorldStateId> states,
        ImmutableArray<WorldStateId> palette,
        ImmutableArray<ushort> paletteIndices,
        ImmutableArray<LogicalSparseRecord> sparseRecords,
        ImmutableArray<LogicalScheduledTick> scheduledTicks)
    {
        Key = key;
        Geometry = geometry;
        Origin = origin;
        EndInclusive = endInclusive;
        States = states;
        Palette = palette;
        PaletteIndices = paletteIndices;
        SparseRecords = sparseRecords;
        ScheduledTicks = scheduledTicks;
    }

    /// <summary>Gets the typed logical identity for this section-state record.</summary>
    public LogicalRecordKey Key { get; }

    /// <summary>Gets the evaluated side and local-index geometry for this record.</summary>
    public SectionGeometry Geometry { get; }

    /// <summary>Gets the checked inclusive block-coordinate origin of the section.</summary>
    public BlockCoord Origin { get; }

    /// <summary>Gets the checked inclusive block-coordinate end of the section.</summary>
    public BlockCoord EndInclusive { get; }

    /// <summary>Gets semantic world-state IDs in X-contiguous, then Z, then Y local-index order.</summary>
    public ImmutableArray<WorldStateId> States { get; }

    /// <summary>Gets exactly the used world-state IDs in ascending numeric order.</summary>
    public ImmutableArray<WorldStateId> Palette { get; }

    /// <summary>Gets one <see cref="Palette"/> offset for every semantic local state, in local-index order.</summary>
    public ImmutableArray<ushort> PaletteIndices { get; }

    /// <summary>Gets sparse records in ascending local-index order.</summary>
    public ImmutableArray<LogicalSparseRecord> SparseRecords { get; }

    /// <summary>Gets scheduled ticks ordered by queue, due tick, priority, and fixture-local sequence.</summary>
    public ImmutableArray<LogicalScheduledTick> ScheduledTicks { get; }

    /// <summary>
    /// Gets the semantic world-state ID at one valid section-relative local coordinate.
    /// </summary>
    /// <param name="local">The local block coordinate.</param>
    /// <returns>The mapped semantic world-state ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="local"/> is outside this geometry.</exception>
    public WorldStateId GetState(LocalBlock local)
    {
        return States[Geometry.GetLinearIndex(local)];
    }

    /// <summary>
    /// Gets the semantic world-state ID at one X-contiguous/Z/Y local index.
    /// </summary>
    /// <param name="localIndex">The zero-based local index.</param>
    /// <returns>The mapped semantic world-state ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="localIndex"/> is outside this record's volume.</exception>
    public WorldStateId GetState(int localIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(localIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(localIndex, States.Length);
        return States[localIndex];
    }

    internal static LogicalSectionRecord Create(LogicalSectionInput input, WorldStateMap worldStates)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(worldStates);

        foreach (WorldStateId state in input.States)
        {
            if (!worldStates.TryGetState(state, out _))
            {
                throw new ArgumentException(
                    $"World state ID {state.Value} is not present in the supplied world-state map.",
                    nameof(input));
            }
        }

        ImmutableArray<WorldStateId> palette = [.. input.States.Distinct().OrderBy(state => state.Value)];
        Dictionary<WorldStateId, ushort> paletteOffsets = new(palette.Length);
        for (int paletteIndex = 0; paletteIndex < palette.Length; paletteIndex++)
        {
            paletteOffsets.Add(palette[paletteIndex], checked((ushort)paletteIndex));
        }

        ImmutableArray<ushort>.Builder paletteIndices = ImmutableArray.CreateBuilder<ushort>(input.States.Length);
        foreach (WorldStateId state in input.States)
        {
            paletteIndices.Add(paletteOffsets[state]);
        }

        SectionGeometry geometry = new(input.Geometry.Side);
        return new LogicalSectionRecord(
            input.Key,
            geometry,
            geometry.GetOrigin(input.Key.Coordinate),
            geometry.GetEndInclusive(input.Key.Coordinate),
            input.States,
            palette,
            paletteIndices.ToImmutable(),
            LogicalSparseRecord.CreateCanonical(input.SparseInputs, input.States.Length),
            LogicalScheduledTick.CreateCanonical(input.ScheduledTickInputs, input.States.Length));
    }
}

/// <summary>
/// Defines copied semantic input for one section-state record before canonical projection construction.
/// </summary>
/// <remarks>
/// This input is storage-neutral fixture data. It is not a persistence, database, migration, wire,
/// or user-world format.
/// </remarks>
public sealed class LogicalSectionInput
{
    /// <summary>
    /// Initializes a section semantic input without sparse records or scheduled ticks.
    /// </summary>
    /// <param name="key">The section-state logical record key.</param>
    /// <param name="geometry">The evaluated section geometry.</param>
    /// <param name="states">Exactly one X-contiguous/Z/Y world-state ID for every local position.</param>
    public LogicalSectionInput(
        LogicalRecordKey key,
        SectionGeometry geometry,
        IEnumerable<WorldStateId> states)
        : this(key, geometry, states, [], [])
    {
    }

    /// <summary>
    /// Initializes a section semantic input and makes immutable copies of every caller-supplied collection.
    /// </summary>
    /// <param name="key">The section-state logical record key.</param>
    /// <param name="geometry">The evaluated section geometry.</param>
    /// <param name="states">Exactly one X-contiguous/Z/Y world-state ID for every local position.</param>
    /// <param name="sparseInputs">Sparse payload inputs for local positions in this section.</param>
    /// <param name="scheduledTicks">Scheduled tick inputs for local positions in this section.</param>
    /// <exception cref="ArgumentException">Thrown when the record kind is not section state or states have the wrong count.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when sparse or scheduled input counts exceed their bounds.</exception>
    /// <exception cref="OverflowException">Thrown when the section origin or inclusive end is not representable.</exception>
    public LogicalSectionInput(
        LogicalRecordKey key,
        SectionGeometry geometry,
        IEnumerable<WorldStateId> states,
        IEnumerable<LogicalSparseInput> sparseInputs,
        IEnumerable<LogicalScheduledTick> scheduledTicks)
    {
        if (key.Kind != LogicalRecordKind.SectionState)
        {
            throw new ArgumentException("A canonical section input requires the SectionState record kind.", nameof(key));
        }

        Geometry = new SectionGeometry(geometry.Side);
        _ = Geometry.GetOrigin(key.Coordinate);
        _ = Geometry.GetEndInclusive(key.Coordinate);

        int volume = GetVolume(Geometry);
        Key = key;
        States = CopyExact(states, volume, nameof(states));
        SparseInputs = CopyBounded(sparseInputs, volume, nameof(sparseInputs));
        ValidateSparseInputs(SparseInputs, volume, nameof(sparseInputs));
        ScheduledTickInputs = CopyBounded(
            scheduledTicks,
            LogicalScheduledTick.MaxTicksPerSection,
            nameof(scheduledTicks));
        ValidateScheduledTicks(ScheduledTickInputs, volume, nameof(scheduledTicks));
    }

    /// <summary>Gets the section-state logical record key.</summary>
    public LogicalRecordKey Key { get; }

    /// <summary>Gets a copied evaluated section geometry.</summary>
    public SectionGeometry Geometry { get; }

    /// <summary>Gets copied semantic world-state IDs in X-contiguous, then Z, then Y order.</summary>
    public ImmutableArray<WorldStateId> States { get; }

    /// <summary>Gets copied sparse semantic inputs in caller order before canonical record sorting.</summary>
    public ImmutableArray<LogicalSparseInput> SparseInputs { get; }

    /// <summary>Gets copied scheduled semantic inputs in caller order before canonical record sorting.</summary>
    public ImmutableArray<LogicalScheduledTick> ScheduledTickInputs { get; }

    private static int GetVolume(SectionGeometry geometry)
    {
        int side = geometry.Side.Value;
        return checked(side * side * side);
    }

    private static ImmutableArray<WorldStateId> CopyExact(
        IEnumerable<WorldStateId> source,
        int expectedCount,
        string parameterName)
    {
        ImmutableArray<WorldStateId> copied = CopyBounded(source, expectedCount, parameterName);
        return copied.Length == expectedCount
            ? copied
            : throw new ArgumentException(
                $"A section requires exactly {expectedCount} semantic states for its geometry.",
                parameterName);
    }

    private static ImmutableArray<T> CopyBounded<T>(IEnumerable<T> source, int maximumCount, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetNonEnumeratedCount(out int suppliedCount) && suppliedCount > maximumCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"At most {maximumCount} entries are permitted.");
        }

        ImmutableArray<T>.Builder copied = ImmutableArray.CreateBuilder<T>();
        foreach (T value in source)
        {
            if (copied.Count == maximumCount)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"At most {maximumCount} entries are permitted.");
            }

            copied.Add(value);
        }

        return copied.ToImmutable();
    }

    private static void ValidateSparseInputs(
        ImmutableArray<LogicalSparseInput> inputs,
        int volume,
        string parameterName)
    {
        for (int index = 0; index < inputs.Length; index++)
        {
            try
            {
                inputs[index].ThrowIfInvalid();
            }
            catch (InvalidOperationException exception)
            {
                throw new ArgumentException($"Sparse input at element {index} is uninitialized or invalid.", parameterName, exception);
            }

            if (inputs[index].LocalIndex >= volume)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    inputs[index].LocalIndex,
                    $"Sparse input at element {index} must use a local index in the range 0 through {volume - 1}.");
            }
        }
    }

    private static void ValidateScheduledTicks(
        ImmutableArray<LogicalScheduledTick> ticks,
        int volume,
        string parameterName)
    {
        for (int index = 0; index < ticks.Length; index++)
        {
            try
            {
                ticks[index].ThrowIfInvalid();
            }
            catch (InvalidOperationException exception)
            {
                throw new ArgumentException($"Scheduled tick at element {index} is uninitialized or invalid.", parameterName, exception);
            }

            if (ticks[index].LocalIndex >= volume)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    ticks[index].LocalIndex,
                    $"Scheduled tick at element {index} must use a local index in the range 0 through {volume - 1}.");
            }
        }
    }
}
