namespace VibeCraft.WorldModel.Sections;

/// <summary>
/// Reports representation facts without estimating runtime-specific object or collection headers.
/// </summary>
internal readonly record struct SectionStorageMetrics(
    SectionBlockStorageKind Kind,
    int VoxelCount,
    int PaletteEntryCount,
    int PaletteCapacity,
    byte BitsPerEntry,
    int PackedWordCount,
    int ReverseLookupEntryCount,
    int OwnedArrayCount,
    long KnownPayloadBytes);
