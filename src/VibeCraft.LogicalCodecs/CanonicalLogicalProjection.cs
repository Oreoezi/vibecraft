using System.Collections.Immutable;
using VibeCraft.Content;

namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Defines one immutable, storage-neutral canonical logical projection for deterministic G1 fixtures.
/// </summary>
/// <remarks>
/// This model is not a persistence, database, migration, wire, or user-world format. It contains
/// semantic values only; a future boundary must define its own explicit versioned codec.
/// </remarks>
public sealed class CanonicalLogicalProjection
{
    /// <summary>The largest number of section-state records admitted by one fixture projection.</summary>
    public const int MaxSectionRecords = 65_536;

    private CanonicalLogicalProjection(
        ImmutableArray<WorldStateBinding> mappingBindings,
        ImmutableArray<LogicalSectionRecord> sections)
    {
        MappingBindings = mappingBindings;
        Sections = sections;
    }

    /// <summary>Gets the world-state bindings in ascending block-state ID order, including preserved gaps.</summary>
    public ImmutableArray<WorldStateBinding> MappingBindings { get; }

    /// <summary>Gets section-state records in canonical logical-record-key order.</summary>
    public ImmutableArray<LogicalSectionRecord> Sections { get; }

    /// <summary>
    /// Creates a canonical projection from one immutable world-state map and section semantic inputs.
    /// </summary>
    /// <param name="worldStates">The validated world-local state mapping used by every supplied semantic state.</param>
    /// <param name="sections">The section semantic inputs, which may arrive in any order.</param>
    /// <returns>A fully validated immutable canonical projection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required input is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when section keys are duplicated or a semantic state is unmapped.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the bounded record count is exceeded.</exception>
    public static CanonicalLogicalProjection Create(
        WorldStateMap worldStates,
        IEnumerable<LogicalSectionInput> sections)
    {
        ArgumentNullException.ThrowIfNull(worldStates);
        ArgumentNullException.ThrowIfNull(sections);

        if (sections.TryGetNonEnumeratedCount(out int suppliedCount) && suppliedCount > MaxSectionRecords)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sections),
                $"A canonical logical projection may contain at most {MaxSectionRecords} section-state records.");
        }

        List<LogicalSectionRecord> records = [];
        HashSet<LogicalRecordKey> keys = [];
        foreach (LogicalSectionInput? section in sections)
        {
            if (records.Count == MaxSectionRecords)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sections),
                    $"A canonical logical projection may contain at most {MaxSectionRecords} section-state records.");
            }

            if (section is null)
            {
                throw new ArgumentException("Section inputs cannot contain null entries.", nameof(sections));
            }

            if (!keys.Add(section.Key))
            {
                throw new ArgumentException($"Logical record key {section.Key} is supplied more than once.", nameof(sections));
            }

            records.Add(LogicalSectionRecord.Create(section, worldStates));
        }

        LogicalSectionRecord[] sorted = [.. records.OrderBy(record => record.Key, LogicalRecordKeyComparer.Instance)];
        return new CanonicalLogicalProjection(worldStates.Bindings, [.. sorted]);
    }
}
