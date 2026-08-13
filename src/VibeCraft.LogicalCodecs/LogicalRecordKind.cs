namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Identifies a logical record family within the storage-neutral G1 identity fixture.
/// </summary>
/// <remarks>
/// This vocabulary does not define a user-world, database, or wire-protocol record kind.
/// </remarks>
public enum LogicalRecordKind : ushort
{
    /// <summary>No logical record family is selected.</summary>
    Undefined = 0,

    /// <summary>Identifies the logical state owned by one section.</summary>
    SectionState = 1,
}
