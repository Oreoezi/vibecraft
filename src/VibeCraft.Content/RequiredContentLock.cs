using System.Collections.Immutable;

namespace VibeCraft.Content;

/// <summary>Identifies one required or resolved gameplay-content provider.</summary>
public readonly record struct ContentProvider
{
    /// <summary>Initializes a provider with validated identity and fingerprint.</summary>
    public ContentProvider(ContentKey key, ContentFingerprint fingerprint)
    {
        key.ThrowIfInvalid();
        fingerprint.ThrowIfInvalid();
        Key = key;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the validated provider key.</summary>
    public ContentKey Key { get; }

    /// <summary>Gets the validated provider fingerprint.</summary>
    public ContentFingerprint Fingerprint { get; }

    internal void ThrowIfInvalid()
    {
        Key.ThrowIfInvalid();
        Fingerprint.ThrowIfInvalid();
    }
}

/// <summary>Classifies a required-provider incompatibility.</summary>
public enum RequiredContentDiagnosticKind
{
    /// <summary>An uninitialized diagnostic kind that is never emitted.</summary>
    Undefined = 0,

    /// <summary>The required provider was not resolved.</summary>
    Missing = 1,

    /// <summary>The provider was resolved with a different fingerprint.</summary>
    Mismatched = 2,
}

/// <summary>Describes one exact missing or mismatched required provider.</summary>
public readonly record struct RequiredContentDiagnostic
{
    private RequiredContentDiagnostic(
        ContentKey provider,
        RequiredContentDiagnosticKind kind,
        ContentFingerprint expected,
        ContentFingerprint? actual)
    {
        provider.ThrowIfInvalid();
        expected.ThrowIfInvalid();
        if (actual is ContentFingerprint actualFingerprint)
        {
            actualFingerprint.ThrowIfInvalid();
        }

        if (kind == RequiredContentDiagnosticKind.Missing && actual is not null)
        {
            throw new ArgumentException("A missing-provider diagnostic cannot contain an actual fingerprint.", nameof(actual));
        }

        if (kind == RequiredContentDiagnosticKind.Mismatched &&
            (actual is not ContentFingerprint mismatchedActual || mismatchedActual.Equals(expected)))
        {
            throw new ArgumentException("A mismatched-provider diagnostic requires a different actual fingerprint.", nameof(actual));
        }

        if (kind is not RequiredContentDiagnosticKind.Missing and not RequiredContentDiagnosticKind.Mismatched)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A defined diagnostic kind is required.");
        }

        Provider = provider;
        Kind = kind;
        Expected = expected;
        Actual = actual;
    }

    /// <summary>Gets the affected provider key.</summary>
    public ContentKey Provider { get; }

    /// <summary>Gets whether the provider is missing or mismatched.</summary>
    public RequiredContentDiagnosticKind Kind { get; }

    /// <summary>Gets the expected provider fingerprint.</summary>
    public ContentFingerprint Expected { get; }

    /// <summary>Gets the actual fingerprint when a provider was found.</summary>
    public ContentFingerprint? Actual { get; }

    internal static RequiredContentDiagnostic Missing(ContentKey provider, ContentFingerprint expected)
    {
        return new RequiredContentDiagnostic(provider, RequiredContentDiagnosticKind.Missing, expected, null);
    }

    internal static RequiredContentDiagnostic Mismatched(
        ContentKey provider,
        ContentFingerprint expected,
        ContentFingerprint actual)
    {
        return new RequiredContentDiagnostic(provider, RequiredContentDiagnosticKind.Mismatched, expected, actual);
    }
}

/// <summary>Contains the pure pre-activation decision produced by required-content validation.</summary>
public sealed class ContentLockValidation
{
    internal ContentLockValidation(ImmutableArray<RequiredContentDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets diagnostics in canonical provider-key order.</summary>
    public ImmutableArray<RequiredContentDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether all required providers are present and compatible.</summary>
    public bool RequirementsSatisfied => Diagnostics.IsEmpty;

    /// <summary>Gets whether this validation blocks activation.</summary>
    public bool BlocksActivation => !RequirementsSatisfied;

    /// <summary>Gets whether this validation blocks source-world writes.</summary>
    public bool BlocksSourceWorldWrites => !RequirementsSatisfied;
}

/// <summary>Validates a required gameplay-content lock without activating or mutating a world.</summary>
public static class RequiredContentLock
{
    /// <summary>The maximum number of providers accepted in either side of one G1 required-content comparison.</summary>
    public const int MaxProviders = 4_096;

    /// <summary>Compares required providers with resolved providers using ordinal key identity and exact fingerprints.</summary>
    public static ContentLockValidation Validate(
        IEnumerable<ContentProvider> requiredProviders,
        IEnumerable<ContentProvider> resolvedProviders)
    {
        ArgumentNullException.ThrowIfNull(requiredProviders);
        ArgumentNullException.ThrowIfNull(resolvedProviders);

        ImmutableDictionary<ContentKey, ContentFingerprint> required = ToUniqueDictionary(requiredProviders, nameof(requiredProviders));
        ImmutableDictionary<ContentKey, ContentFingerprint> resolved = ToUniqueDictionary(resolvedProviders, nameof(resolvedProviders));
        ImmutableArray<RequiredContentDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<RequiredContentDiagnostic>();

        foreach ((ContentKey key, ContentFingerprint expected) in required.OrderBy(pair => pair.Key))
        {
            if (!resolved.TryGetValue(key, out ContentFingerprint actual))
            {
                diagnostics.Add(RequiredContentDiagnostic.Missing(key, expected));
            }
            else if (!expected.Equals(actual))
            {
                diagnostics.Add(RequiredContentDiagnostic.Mismatched(key, expected, actual));
            }
        }

        return new ContentLockValidation(diagnostics.ToImmutable());
    }

    private static ImmutableDictionary<ContentKey, ContentFingerprint> ToUniqueDictionary(
        IEnumerable<ContentProvider> providers,
        string parameterName)
    {
        if (providers.TryGetNonEnumeratedCount(out int count) && count > MaxProviders)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"A required-content provider set may contain at most {MaxProviders} providers.");
        }

        ImmutableDictionary<ContentKey, ContentFingerprint>.Builder result = ImmutableDictionary.CreateBuilder<ContentKey, ContentFingerprint>();
        int inspectedCount = 0;
        foreach (ContentProvider provider in providers)
        {
            if (inspectedCount == MaxProviders)
            {
                throw new ArgumentOutOfRangeException(parameterName, $"A required-content provider set may contain at most {MaxProviders} providers.");
            }

            inspectedCount++;
            provider.ThrowIfInvalid();
            if (!result.TryAdd(provider.Key, provider.Fingerprint))
            {
                throw new ArgumentException($"Provider {provider.Key} appears more than once.", parameterName);
            }
        }

        return result.ToImmutable();
    }
}

/// <summary>Defines the content-lock result carried into world opening; it never grants storage authority.</summary>
public sealed class WorldOpenDecision
{
    private WorldOpenDecision(ImmutableArray<RequiredContentDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the exact immutable required-content diagnostics carried from validation.</summary>
    public ImmutableArray<RequiredContentDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether all required providers are present and compatible.</summary>
    public bool RequirementsSatisfied => Diagnostics.IsEmpty;

    /// <summary>Gets whether this content gate blocks activation.</summary>
    public bool BlocksActivation => !RequirementsSatisfied;

    /// <summary>Gets whether this content gate blocks source-world writes.</summary>
    public bool BlocksSourceWorldWrites => !RequirementsSatisfied;

    /// <summary>Creates a side-effect-free open decision from a content-lock validation result.</summary>
    public static WorldOpenDecision From(ContentLockValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return new WorldOpenDecision(validation.Diagnostics);
    }
}
