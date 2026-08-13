using System.Collections.Immutable;

namespace VibeCraft.Content;

/// <summary>Defines the only operations available to G1 unresolved-content recovery.</summary>
[Flags]
public enum RecoveryCapability
{
    /// <summary>Permits inspection of bounded preserved data.</summary>
    Inspect = 1,

    /// <summary>Permits exporting bounded preserved data.</summary>
    Export = 2,
}

/// <summary>Preserves a bounded unresolved block state for explicit recovery or export only.</summary>
public sealed class UnresolvedBlockState
{
    /// <summary>The maximum number of opaque saved-definition metadata bytes retained by G1 recovery.</summary>
    public const int MaxSavedDefinitionMetadataBytes = 1024;

    private UnresolvedBlockState(
        CanonicalBlockState originalState,
        ContentFingerprint savedDefinitionFingerprint,
        ImmutableArray<byte> savedDefinitionMetadata)
    {
        OriginalState = originalState;
        SavedDefinitionFingerprint = savedDefinitionFingerprint;
        SavedDefinitionMetadata = savedDefinitionMetadata;
    }

    /// <summary>Gets the original logical state; it is never replaced with air.</summary>
    public CanonicalBlockState OriginalState { get; }

    /// <summary>Gets the fingerprint saved for the unresolved definition.</summary>
    public ContentFingerprint SavedDefinitionFingerprint { get; }

    /// <summary>Gets bounded opaque saved-definition metadata.</summary>
    public ImmutableArray<byte> SavedDefinitionMetadata { get; }

    /// <summary>Creates a bounded recovery record without trusting unresolved content as executable gameplay data.</summary>
    public static UnresolvedBlockState Create(
        CanonicalBlockState originalState,
        ContentFingerprint savedDefinitionFingerprint,
        ReadOnlySpan<byte> savedDefinitionMetadata)
    {
        ArgumentNullException.ThrowIfNull(originalState);
        savedDefinitionFingerprint.ThrowIfInvalid();
        return originalState.Equals(CanonicalBlockState.Air)
            ? throw new ArgumentException("Playable air cannot be represented as unresolved content.", nameof(originalState))
            : savedDefinitionMetadata.Length > MaxSavedDefinitionMetadataBytes
            ? throw new ArgumentOutOfRangeException(
                nameof(savedDefinitionMetadata),
                $"Recovery metadata may contain at most {MaxSavedDefinitionMetadataBytes} bytes.")
            : new UnresolvedBlockState(originalState, savedDefinitionFingerprint, [.. savedDefinitionMetadata]);
    }
}

/// <summary>Defines a non-playable, source-write-forbidden recovery/export access mode.</summary>
public sealed class RecoveryWorldAccess
{
    private RecoveryWorldAccess(UnresolvedBlockState unresolvedState, RecoveryCapability capabilities)
    {
        if (capabilities != (RecoveryCapability.Inspect | RecoveryCapability.Export))
        {
            throw new ArgumentOutOfRangeException(nameof(capabilities), "Recovery access supports inspection and export only.");
        }

        UnresolvedState = unresolvedState;
        Capabilities = capabilities;
    }

    /// <summary>Gets the bounded unresolved state being inspected or exported.</summary>
    public UnresolvedBlockState UnresolvedState { get; }

    /// <summary>Gets the exact allowed recovery operation set: inspection and export only.</summary>
    public RecoveryCapability Capabilities { get; }

    /// <summary>Creates recovery/export access with no simulation or source-write capability.</summary>
    public static RecoveryWorldAccess Create(UnresolvedBlockState unresolvedState)
    {
        ArgumentNullException.ThrowIfNull(unresolvedState);
        return new RecoveryWorldAccess(unresolvedState, RecoveryCapability.Inspect | RecoveryCapability.Export);
    }
}
