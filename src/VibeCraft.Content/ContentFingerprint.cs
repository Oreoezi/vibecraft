using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace VibeCraft.Content;

/// <summary>Identifies one validated, extensible typed registry domain.</summary>
public readonly struct ContentRegistryId : IEquatable<ContentRegistryId>
{
    private readonly ContentKey identity;

    private ContentRegistryId(ContentKey identity)
    {
        this.identity = identity;
    }

    /// <summary>Gets the built-in block definition registry identity.</summary>
    public static ContentRegistryId Block { get; } = Parse("vibecraft:registry/block");

    /// <summary>Gets the built-in item definition registry identity.</summary>
    public static ContentRegistryId Item { get; } = Parse("vibecraft:registry/item");

    /// <summary>Gets the built-in entity-type definition registry identity.</summary>
    public static ContentRegistryId EntityType { get; } = Parse("vibecraft:registry/entity_type");

    /// <summary>Gets the built-in block-entity-type definition registry identity.</summary>
    public static ContentRegistryId BlockEntityType { get; } = Parse("vibecraft:registry/block_entity_type");

    /// <summary>Gets the built-in biome definition registry identity.</summary>
    public static ContentRegistryId Biome { get; } = Parse("vibecraft:registry/biome");

    /// <summary>Gets the built-in recipe definition registry identity.</summary>
    public static ContentRegistryId Recipe { get; } = Parse("vibecraft:registry/recipe");

    /// <summary>Gets the built-in structure definition registry identity.</summary>
    public static ContentRegistryId Structure { get; } = Parse("vibecraft:registry/structure");

    /// <summary>Gets the built-in block-tag registry identity.</summary>
    public static ContentRegistryId BlockTag { get; } = Parse("vibecraft:registry/tag/block");

    /// <summary>Gets the built-in item-tag registry identity.</summary>
    public static ContentRegistryId ItemTag { get; } = Parse("vibecraft:registry/tag/item");

    /// <summary>Gets the built-in entity-type-tag registry identity.</summary>
    public static ContentRegistryId EntityTypeTag { get; } = Parse("vibecraft:registry/tag/entity_type");

    /// <summary>Gets the built-in block-entity-type-tag registry identity.</summary>
    public static ContentRegistryId BlockEntityTypeTag { get; } = Parse("vibecraft:registry/tag/block_entity_type");

    /// <summary>Gets the built-in biome-tag registry identity.</summary>
    public static ContentRegistryId BiomeTag { get; } = Parse("vibecraft:registry/tag/biome");

    /// <summary>Gets the built-in recipe-tag registry identity.</summary>
    public static ContentRegistryId RecipeTag { get; } = Parse("vibecraft:registry/tag/recipe");

    /// <summary>Gets the built-in structure-tag registry identity.</summary>
    public static ContentRegistryId StructureTag { get; } = Parse("vibecraft:registry/tag/structure");

    /// <summary>Gets the trusted behavior-implementation compatibility-ID registry identity.</summary>
    public static ContentRegistryId BehaviorImplementation { get; } = Parse("vibecraft:registry/behavior_implementation");

    /// <summary>Gets the generator-relevant content registry identity.</summary>
    public static ContentRegistryId GeneratorContent { get; } = Parse("vibecraft:registry/generator_content");

    /// <summary>Gets whether this value contains a validated namespaced registry identity.</summary>
    public bool IsValid => identity.IsValid;

    /// <summary>Creates an extensible registry identity from a validated namespaced key.</summary>
    public static ContentRegistryId Create(ContentKey identity)
    {
        identity.ThrowIfInvalid();
        return new ContentRegistryId(identity);
    }

    /// <summary>Parses an extensible registry identity from a canonical namespaced key.</summary>
    public static ContentRegistryId Parse(string value)
    {
        return Create(ContentKey.Parse(value));
    }

    /// <inheritdoc />
    public bool Equals(ContentRegistryId other)
    {
        return identity.Equals(other.identity);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ContentRegistryId other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return identity.GetHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsValid ? identity.ToString() : "<invalid-content-registry>";
    }

    internal void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("ContentRegistryId is uninitialized or invalid.");
        }
    }

    /// <summary>Compares two registry identities.</summary>
    public static bool operator ==(ContentRegistryId left, ContentRegistryId right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two registry identities.</summary>
    public static bool operator !=(ContentRegistryId left, ContentRegistryId right)
    {
        return !left.Equals(right);
    }
}

/// <summary>Supplies one explicitly canonicalized typed-registry entry for a content fingerprint.</summary>
public sealed class ContentFingerprintEntry
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The G1 maximum UTF-8 byte length for one canonical definition.</summary>
    public const int MaxCanonicalDefinitionUtf8Bytes = 64 * 1024;

    private ContentFingerprintEntry(
        ContentRegistryId registry,
        ContentKey key,
        string canonicalDefinition)
    {
        Registry = registry;
        Key = key;
        CanonicalDefinition = canonicalDefinition;
    }

    /// <summary>Gets the typed registry domain.</summary>
    public ContentRegistryId Registry { get; }

    /// <summary>Gets the validated provider or definition key.</summary>
    public ContentKey Key { get; }

    /// <summary>Gets the validated canonical definition text.</summary>
    public string CanonicalDefinition { get; }

    /// <summary>Validates a fingerprint entry.</summary>
    public static ContentFingerprintEntry Create(
        ContentRegistryId registry,
        ContentKey key,
        string canonicalDefinition)
    {
        ArgumentException.ThrowIfNullOrEmpty(canonicalDefinition);
        registry.ThrowIfInvalid();
        key.ThrowIfInvalid();
        return StrictUtf8.GetByteCount(canonicalDefinition) > MaxCanonicalDefinitionUtf8Bytes
            ? throw new ArgumentOutOfRangeException(
                nameof(canonicalDefinition),
                $"Canonical definitions may contain at most {MaxCanonicalDefinitionUtf8Bytes} UTF-8 bytes.")
            : new ContentFingerprintEntry(registry, key, canonicalDefinition);
    }
}

/// <summary>Defines an explicit, deterministic canonical input for content fingerprinting.</summary>
public sealed class ContentFingerprintInput
{
    private const string CanonicalHeader = "vibecraft-content-fingerprint-v2\n";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private ContentFingerprintInput(
        ImmutableArray<ContentFingerprintEntry> entries,
        string canonicalInput,
        int canonicalInputUtf8ByteCount)
    {
        Entries = entries;
        CanonicalInput = canonicalInput;
        CanonicalInputUtf8ByteCount = canonicalInputUtf8ByteCount;
    }

    /// <summary>The maximum number of entries accepted by one G1 content fingerprint input.</summary>
    public const int MaxEntries = 1_048_576;

    /// <summary>
    /// The maximum UTF-8 byte length of the complete canonical input, including header and length prefixes.
    /// This 64 MiB bound keeps a G1 fingerprint projection memory-bounded while allowing the entry-count guard
    /// to remain independently reachable with minimal valid typed entries.
    /// </summary>
    public const int MaxCanonicalInputUtf8Bytes = 64 * 1024 * 1024;

    /// <summary>Gets entries in canonical ordinal key order.</summary>
    public ImmutableArray<ContentFingerprintEntry> Entries { get; }

    /// <summary>
    /// Gets the exact UTF-8 text hashed by <see cref="ContentFingerprint.Compute(ContentFingerprintInput)"/>.
    /// Each key and definition is length-prefixed by its UTF-8 byte count.
    /// </summary>
    public string CanonicalInput { get; }

    /// <summary>Gets the exact validated UTF-8 byte length of <see cref="CanonicalInput"/>.</summary>
    public int CanonicalInputUtf8ByteCount { get; }

    /// <summary>Creates a canonical input, rejecting duplicate provider/definition keys.</summary>
    public static ContentFingerprintInput Create(IEnumerable<ContentFingerprintEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        List<ContentFingerprintEntry> supplied = [];
        int canonicalInputUtf8ByteCount = StrictUtf8.GetByteCount(CanonicalHeader);
        foreach (ContentFingerprintEntry entry in entries)
        {
            if (entry is null)
            {
                throw new ArgumentException("Fingerprint input cannot contain null entries.", nameof(entries));
            }

            if (supplied.Count == MaxEntries)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), $"Fingerprint input may contain at most {MaxEntries} entries.");
            }

            int entryUtf8ByteCount = GetEncodedEntryByteCount(
                entry.Registry.ToString(),
                entry.Key.ToString(),
                entry.CanonicalDefinition);
            if (entryUtf8ByteCount > MaxCanonicalInputUtf8Bytes - canonicalInputUtf8ByteCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    $"Canonical fingerprint input may contain at most {MaxCanonicalInputUtf8Bytes} UTF-8 bytes.");
            }

            canonicalInputUtf8ByteCount += entryUtf8ByteCount;
            supplied.Add(entry);
        }

        ContentFingerprintEntry[] ordered =
        [
            .. supplied
                .OrderBy(entry => entry.Registry.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key),
        ];
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].Registry.Equals(ordered[index].Registry) &&
                ordered[index - 1].Key.Equals(ordered[index].Key))
            {
                throw new ArgumentException(
                    $"Fingerprint input contains duplicate ({ordered[index].Registry}, {ordered[index].Key}) identity.",
                    nameof(entries));
            }
        }

        StringBuilder canonical = new(CanonicalHeader);
        foreach (ContentFingerprintEntry entry in ordered)
        {
            AppendLengthPrefixed(canonical, entry.Registry.ToString());
            AppendLengthPrefixed(canonical, entry.Key.ToString());
            AppendLengthPrefixed(canonical, entry.CanonicalDefinition);
        }

        return new ContentFingerprintInput([.. ordered], canonical.ToString(), canonicalInputUtf8ByteCount);
    }

    private static void AppendLengthPrefixed(StringBuilder builder, string value)
    {
        _ = builder.Append(StrictUtf8.GetByteCount(value))
            .Append(':')
            .Append(value)
            .Append('\n');
    }

    private static int GetEncodedEntryByteCount(string registryIdentity, string key, string definition)
    {
        int registryBytes = StrictUtf8.GetByteCount(registryIdentity);
        int keyBytes = StrictUtf8.GetByteCount(key);
        int definitionBytes = StrictUtf8.GetByteCount(definition);
        return checked(
            registryBytes + CountDecimalDigits(registryBytes) + 2 +
            keyBytes + CountDecimalDigits(keyBytes) + 2 +
            definitionBytes + CountDecimalDigits(definitionBytes) + 2);
    }

    private static int CountDecimalDigits(int value)
    {
        return value switch
        {
            < 10 => 1,
            < 100 => 2,
            < 1_000 => 3,
            < 10_000 => 4,
            < 100_000 => 5,
            _ => 6,
        };
    }
}

/// <summary>Defines a SHA-256 digest over an explicit canonical content input.</summary>
public readonly record struct ContentFingerprint
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private ContentFingerprint(string sha256Hex)
    {
        Sha256Hex = sha256Hex;
    }

    /// <summary>Gets the canonical lowercase hexadecimal SHA-256 digest.</summary>
    public string Sha256Hex { get; }

    /// <summary>Gets whether this value is a validated lowercase SHA-256 digest.</summary>
    public bool IsValid => Sha256Hex is not null && Sha256Hex.Length == 64 && Sha256Hex.All(IsLowerHexadecimal);

    /// <summary>Computes a fingerprint from the input's explicit canonical text encoded as UTF-8.</summary>
    public static ContentFingerprint Compute(ContentFingerprintInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        byte[] digest = SHA256.HashData(StrictUtf8.GetBytes(input.CanonicalInput));
        return new ContentFingerprint(Convert.ToHexStringLower(digest));
    }

    /// <summary>Creates a fingerprint from an already-known canonical lowercase SHA-256 digest.</summary>
    public static ContentFingerprint ParseSha256Hex(string sha256Hex)
    {
        ArgumentNullException.ThrowIfNull(sha256Hex);
        return sha256Hex.Length != 64 || !sha256Hex.All(value => value is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? throw new FormatException("A content fingerprint must be 64 lowercase hexadecimal characters.")
            : new ContentFingerprint(sha256Hex);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsValid ? Sha256Hex : "<invalid-content-fingerprint>";
    }

    internal void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("ContentFingerprint is uninitialized or invalid.");
        }
    }

    private static bool IsLowerHexadecimal(char value)
    {
        return value is (>= '0' and <= '9') or (>= 'a' and <= 'f');
    }
}
