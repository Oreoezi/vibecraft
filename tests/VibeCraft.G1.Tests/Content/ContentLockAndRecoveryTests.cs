using System.Reflection;
using System.Text;
using VibeCraft.Content;
using Xunit;

namespace VibeCraft.G1.Tests.Content;

public sealed class ContentLockAndRecoveryTests
{
    [Fact]
    public void FingerprintsUseExplicitStableCanonicalInputAcrossDiscoveryOrder()
    {
        ContentFingerprintEntry first = ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            ContentKey.Parse("vibecraft:stone"),
            "vibecraft:stone");
        ContentFingerprintEntry second = ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            ContentKey.Parse("example:ore"),
            "example:ore[example:grade=rich]");

        ContentFingerprintInput ordered = ContentFingerprintInput.Create([first, second]);
        ContentFingerprintInput shuffled = ContentFingerprintInput.Create([second, first]);

        Assert.Equal(ordered.CanonicalInput, shuffled.CanonicalInput);
        ContentFingerprint fingerprint = ContentFingerprint.Compute(ordered);
        Assert.Equal(fingerprint, ContentFingerprint.Compute(shuffled));
        Assert.Equal("881860128f1b7d79a012adf855de56af427abe543ea5a22b7b9962b2d36e18ad", fingerprint.Sha256Hex);
        Assert.StartsWith("vibecraft-content-fingerprint-v2\n", ordered.CanonicalInput, StringComparison.Ordinal);
        Assert.Contains("24:vibecraft:registry/block\n", ordered.CanonicalInput, StringComparison.Ordinal);
        Assert.Contains("15:vibecraft:stone\n", ordered.CanonicalInput, StringComparison.Ordinal);
    }

    [Fact]
    public void FingerprintIdentityIncludesTypedRegistryAndRejectsOnlyDuplicateRegistryKeyPairs()
    {
        ContentKey sharedKey = ContentKey.Parse("vibecraft:shared");
        ContentFingerprintEntry block = ContentFingerprintEntry.Create(ContentRegistryId.Block, sharedKey, "block-definition");
        ContentFingerprintEntry blockTag = ContentFingerprintEntry.Create(ContentRegistryId.BlockTag, sharedKey, "block-tag-definition");
        ContentFingerprintEntry behavior = ContentFingerprintEntry.Create(
            ContentRegistryId.BehaviorImplementation,
            sharedKey,
            "behavior-compatibility-id");

        ContentFingerprintInput typed = ContentFingerprintInput.Create([blockTag, behavior, block]);
        ContentFingerprintInput shuffled = ContentFingerprintInput.Create([behavior, block, blockTag]);

        Assert.Equal(
            [ContentRegistryId.BehaviorImplementation, ContentRegistryId.Block, ContentRegistryId.BlockTag],
            typed.Entries.Select(entry => entry.Registry));
        Assert.Equal(typed.CanonicalInput, shuffled.CanonicalInput);
        Assert.Equal(ContentFingerprint.Compute(typed), ContentFingerprint.Compute(shuffled));
        _ = Assert.Throws<ArgumentException>(() => ContentFingerprintInput.Create([block, block]));
    }

    [Fact]
    public void RequiredContentFailureReportsExactSortedDiagnosticsAndForbidsActivationAndWrites()
    {
        ContentFingerprint expectedA = Fingerprint('a');
        ContentFingerprint expectedB = Fingerprint('b');
        ContentFingerprint actualB = Fingerprint('c');
        ContentProvider[] required =
        [
            new ContentProvider(ContentKey.Parse("zmod:missing"), expectedA),
            new ContentProvider(ContentKey.Parse("amod:mismatch"), expectedB),
        ];
        ContentProvider[] resolved = [new ContentProvider(ContentKey.Parse("amod:mismatch"), actualB)];

        ContentLockValidation validation = RequiredContentLock.Validate(required, resolved);
        WorldOpenDecision decision = WorldOpenDecision.From(validation);

        Assert.False(validation.RequirementsSatisfied);
        Assert.True(validation.BlocksActivation);
        Assert.True(validation.BlocksSourceWorldWrites);
        Assert.False(decision.RequirementsSatisfied);
        Assert.True(decision.BlocksActivation);
        Assert.True(decision.BlocksSourceWorldWrites);
        Assert.Equal(validation.Diagnostics, decision.Diagnostics);
        Assert.Equal(
            [RequiredContentDiagnosticKind.Mismatched, RequiredContentDiagnosticKind.Missing],
            validation.Diagnostics.Select(diagnostic => diagnostic.Kind));
        Assert.Equal(
            ["amod:mismatch", "zmod:missing"],
            decision.Diagnostics.Select(diagnostic => diagnostic.Provider.ToString()));
        Assert.Equal("amod:mismatch", validation.Diagnostics[0].Provider.ToString());
        Assert.Equal(expectedB, validation.Diagnostics[0].Expected);
        Assert.Equal(actualB, validation.Diagnostics[0].Actual);
        Assert.Equal("zmod:missing", validation.Diagnostics[1].Provider.ToString());
        Assert.Equal(expectedA, validation.Diagnostics[1].Expected);
        Assert.Null(validation.Diagnostics[1].Actual);
    }

    [Fact]
    public void MatchingRequiredContentAllowsPreActivationTransition()
    {
        ContentProvider provider = new(ContentKey.Parse("vibecraft:base"), Fingerprint('d'));

        ContentLockValidation validation = RequiredContentLock.Validate([provider], [provider]);

        Assert.True(validation.RequirementsSatisfied);
        Assert.False(validation.BlocksActivation);
        Assert.False(validation.BlocksSourceWorldWrites);
        WorldOpenDecision decision = WorldOpenDecision.From(validation);
        Assert.True(decision.RequirementsSatisfied);
        Assert.False(decision.BlocksActivation);
        Assert.False(decision.BlocksSourceWorldWrites);
    }

    [Fact]
    public void UnresolvedStateIsBoundedRecoveryDataNotPlayableAirOrAWriteAuthority()
    {
        CanonicalBlockState missing = new(
            ContentKey.Parse("missing:machine"),
            [BlockStateProperty.Create(ContentKey.Parse("missing:facing"), "north")]);
        UnresolvedBlockState unresolved = UnresolvedBlockState.Create(missing, Fingerprint('e'), [1, 2, 3]);
        RecoveryWorldAccess recovery = RecoveryWorldAccess.Create(unresolved);

        Assert.Equal(missing, unresolved.OriginalState);
        Assert.NotEqual(CanonicalBlockState.Air, unresolved.OriginalState);
        Assert.Equal(RecoveryCapability.Inspect | RecoveryCapability.Export, recovery.Capabilities);
        Assert.Equal([RecoveryCapability.Inspect, RecoveryCapability.Export], Enum.GetValues<RecoveryCapability>());
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => UnresolvedBlockState.Create(
            missing,
            Fingerprint('e'),
            new byte[UnresolvedBlockState.MaxSavedDefinitionMetadataBytes + 1]));
        _ = Assert.Throws<ArgumentException>(() => UnresolvedBlockState.Create(CanonicalBlockState.Air, Fingerprint('e'), []));
    }

    [Fact]
    public void DefaultFingerprintIsExplicitlyInvalidAndRejectedAtEveryFingerprintBoundary()
    {
        ContentFingerprint invalid = default;
        CanonicalBlockState state = new(ContentKey.Parse("missing:machine"), []);

        Assert.False(invalid.IsValid);
        Assert.Equal("<invalid-content-fingerprint>", invalid.ToString());
        _ = Assert.Throws<InvalidOperationException>(() => new ContentProvider(ContentKey.Parse("vibecraft:base"), invalid));
        _ = Assert.Throws<InvalidOperationException>(() => UnresolvedBlockState.Create(state, invalid, []));
        _ = Assert.Throws<InvalidOperationException>(() => RequiredContentLock.Validate(
            [default],
            []));
    }

    [Fact]
    public void FingerprintEntriesRejectInvalidKeysEmptyOrOversizedUtf8DefinitionsAndNullInputEntries()
    {
        ContentKey key = ContentKey.Parse("vibecraft:stone");

        _ = Assert.Throws<ArgumentException>(() => ContentFingerprintEntry.Create(ContentRegistryId.Block, key, string.Empty));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            key,
            new string('x', ContentFingerprintEntry.MaxCanonicalDefinitionUtf8Bytes + 1)));
        ContentRegistryId invalidRegistry = default;
        Assert.False(invalidRegistry.IsValid);
        Assert.Equal("<invalid-content-registry>", invalidRegistry.ToString());
        _ = Assert.Throws<InvalidOperationException>(() => ContentFingerprintEntry.Create(
            invalidRegistry,
            key,
            "definition"));
        ContentRegistryId extensionRegistry = ContentRegistryId.Parse("example:registry/custom_gameplay");
        Assert.True(extensionRegistry.IsValid);
        Assert.Equal("example:registry/custom_gameplay", extensionRegistry.ToString());
        ContentFingerprintEntry?[] entries = [null];
        _ = Assert.Throws<ArgumentException>(() => ContentFingerprintInput.Create(entries!));
        Assert.Empty(typeof(ContentFingerprintEntry).GetConstructors());
    }

    [Fact]
    public void FingerprintEntriesRejectInvalidUtf16InsteadOfCollidingWithReplacementCharacter()
    {
        ContentKey key = ContentKey.Parse("vibecraft:stone");
        ContentFingerprintEntry replacement = ContentFingerprintEntry.Create(ContentRegistryId.Block, key, "\uFFFD");

        _ = Assert.Throws<EncoderFallbackException>(() => ContentFingerprintEntry.Create(ContentRegistryId.Block, key, "\uD800"));
        ContentFingerprintInput input = ContentFingerprintInput.Create([replacement]);
        Assert.NotEqual(default, ContentFingerprint.Compute(input));
    }

    [Fact]
    public void FingerprintInputBoundsEntryCountAndTotalCanonicalBytesDuringEnumeration()
    {
        ContentFingerprintEntry single = ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            ContentKey.Parse("a:b"),
            "x");
        ContentFingerprintInput small = ContentFingerprintInput.Create([single]);
        string maximumDefinition = new('x', ContentFingerprintEntry.MaxCanonicalDefinitionUtf8Bytes);
        ContentFingerprintEntry maximumEntry = ContentFingerprintEntry.Create(
            ContentRegistryId.Block,
            ContentKey.Parse("a:b"),
            maximumDefinition);
        CountingRepeatEnumerable<ContentFingerprintEntry> overByteLimit = new(maximumEntry, 1_024);

        Assert.InRange(small.CanonicalInputUtf8ByteCount, 1, ContentFingerprintInput.MaxCanonicalInputUtf8Bytes);
        ArgumentOutOfRangeException byteError = Assert.Throws<ArgumentOutOfRangeException>(
            () => ContentFingerprintInput.Create(overByteLimit));
        Assert.Contains("UTF-8 bytes", byteError.Message, StringComparison.Ordinal);
        Assert.Equal(1_024, overByteLimit.YieldCount);

        CountingRepeatEnumerable<ContentFingerprintEntry> overEntryLimit = new(
            single,
            ContentFingerprintInput.MaxEntries + 1);
        ArgumentOutOfRangeException countError = Assert.Throws<ArgumentOutOfRangeException>(
            () => ContentFingerprintInput.Create(overEntryLimit));
        Assert.Contains($"at most {ContentFingerprintInput.MaxEntries} entries", countError.Message, StringComparison.Ordinal);
        Assert.Equal(ContentFingerprintInput.MaxEntries + 1, overEntryLimit.YieldCount);
    }

    [Fact]
    public void RequiredContentProviderSetsAreBoundedAndStopAtTheFirstOverLimitEntry()
    {
        ContentFingerprint fingerprint = Fingerprint('f');
        ContentProvider[] atLimit =
        [
            .. Enumerable.Range(0, RequiredContentLock.MaxProviders)
                .Select(index => new ContentProvider(ContentKey.Parse($"provider:p_{index}"), fingerprint)),
        ];
        int yielded = 0;

        ContentLockValidation validation = RequiredContentLock.Validate(atLimit, []);
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => RequiredContentLock.Validate(OverLimit(), []));

        Assert.Equal(RequiredContentLock.MaxProviders, validation.Diagnostics.Length);
        Assert.Contains($"at most {RequiredContentLock.MaxProviders} providers", error.Message, StringComparison.Ordinal);
        Assert.Equal(RequiredContentLock.MaxProviders + 1, yielded);

        IEnumerable<ContentProvider> OverLimit()
        {
            for (int index = 0; index <= RequiredContentLock.MaxProviders; index++)
            {
                yielded++;
                yield return new ContentProvider(ContentKey.Parse($"provider:overflow_{index}"), fingerprint);
            }
        }
    }

    [Fact]
    public void RequiredContentDiagnosticsCanOnlyBeCreatedWithValidKindActualRelationships()
    {
        Assert.Empty(typeof(RequiredContentDiagnostic).GetConstructors());
        ConstructorInfo constructor = Assert.Single(typeof(RequiredContentDiagnostic).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        ContentKey provider = ContentKey.Parse("vibecraft:base");
        ContentFingerprint expected = Fingerprint('a');

        AssertConstructorRejects(constructor, provider, RequiredContentDiagnosticKind.Undefined, expected, null);
        AssertConstructorRejects(constructor, provider, RequiredContentDiagnosticKind.Missing, expected, Fingerprint('b'));
        AssertConstructorRejects(constructor, provider, RequiredContentDiagnosticKind.Mismatched, expected, null);
        AssertConstructorRejects(constructor, provider, RequiredContentDiagnosticKind.Mismatched, expected, expected);
    }

    [Fact]
    public void CriticalTypesExposeNoPublicBypassableConstructors()
    {
        Assert.Empty(typeof(WorldOpenDecision).GetConstructors());
        Assert.Empty(typeof(RecoveryWorldAccess).GetConstructors());
    }

    private static ContentFingerprint Fingerprint(char character)
    {
        return ContentFingerprint.ParseSha256Hex(new string(character, 64));
    }

    private static void AssertConstructorRejects(
        ConstructorInfo constructor,
        ContentKey provider,
        RequiredContentDiagnosticKind kind,
        ContentFingerprint expected,
        ContentFingerprint? actual)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => constructor.Invoke([provider, kind, expected, actual]));
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    private sealed class CountingRepeatEnumerable<T>(T value, int count) : IEnumerable<T>
    {
        public int YieldCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            for (int index = 0; index < count; index++)
            {
                YieldCount++;
                yield return value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
