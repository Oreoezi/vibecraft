using System.Collections.Immutable;

namespace VibeCraft.Content;

/// <summary>Identifies one state in a world's block-state mapping.</summary>
public readonly record struct BlockStateId(uint Value);

/// <summary>Identifies one resolved state for the lifetime of one runtime snapshot.</summary>
public readonly record struct RuntimeStateId(uint Value);

/// <summary>Identifies one state for the lifetime of one negotiated session mapping.</summary>
public readonly record struct SessionStateId(uint Value);

/// <summary>Defines one finite canonical block-state property.</summary>
public readonly record struct BlockStateProperty
{
    private BlockStateProperty(NamespacedContentId key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>The largest permitted property-value length.</summary>
    public const int MaxValueLength = 64;

    /// <summary>Gets the canonical property key.</summary>
    public NamespacedContentId Key { get; }

    /// <summary>Gets the canonical property value.</summary>
    public string Value { get; }

    /// <summary>Gets whether this value is a validated canonical property.</summary>
    public bool IsValid => Key.IsValid && Value is not null && Value.Length is > 0 and <= MaxValueLength && Value.All(IsValueCharacter);

    /// <summary>Creates a validated canonical property.</summary>
    public static BlockStateProperty Create(NamespacedContentId key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        key.ThrowIfInvalid();
        return value.Length is 0 or > MaxValueLength || !value.All(IsValueCharacter)
            ? throw new ArgumentException("Block-state property values must match [a-z0-9_.-]+ and be length-bounded.", nameof(value))
            : new BlockStateProperty(key, value);
    }

    private static bool IsValueCharacter(char value)
    {
        return value is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.' or '-';
    }
}

/// <summary>Defines the durable logical identity of one finite block state.</summary>
public sealed class CanonicalBlockState : IEquatable<CanonicalBlockState>, IComparable<CanonicalBlockState>
{
    private static readonly NamespacedContentId AirKey = NamespacedContentId.Parse("vibecraft:air");

    /// <summary>The maximum number of properties in one G1 block-state identity.</summary>
    public const int MaxProperties = 32;

    /// <summary>The unique canonical air state.</summary>
    public static CanonicalBlockState Air { get; } = new(AirKey, []);

    /// <summary>Initializes a block state and canonicalizes its properties by ordinal property key.</summary>
    public CanonicalBlockState(NamespacedContentId block, IEnumerable<BlockStateProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        block.ThrowIfInvalid();
        Block = block;

        List<BlockStateProperty> supplied = [];
        foreach (BlockStateProperty property in properties)
        {
            if (supplied.Count == MaxProperties)
            {
                throw new ArgumentOutOfRangeException(nameof(properties), $"A block state may have at most {MaxProperties} properties.");
            }

            if (!property.IsValid)
            {
                throw new ArgumentException("Block-state properties must be initialized and valid.", nameof(properties));
            }

            supplied.Add(property);
        }

        BlockStateProperty[] sorted = [.. supplied.OrderBy(property => property.Key)];

        for (int index = 1; index < sorted.Length; index++)
        {
            if (sorted[index - 1].Key.Equals(sorted[index].Key))
            {
                throw new ArgumentException("A block-state property key may appear only once.", nameof(properties));
            }
        }

        if (block.Equals(AirKey) && sorted.Length != 0)
        {
            throw new ArgumentException("vibecraft:air has exactly one state with no properties.", nameof(properties));
        }

        Properties = [.. sorted];
    }

    /// <summary>Gets the block's stable namespaced content identifier.</summary>
    public NamespacedContentId Block { get; }

    /// <summary>Gets properties in canonical ordinal key order.</summary>
    public ImmutableArray<BlockStateProperty> Properties { get; }

    /// <inheritdoc />
    public int CompareTo(CanonicalBlockState? other)
    {
        if (other is null)
        {
            return 1;
        }

        int blockComparison = Block.CompareTo(other.Block);
        if (blockComparison != 0)
        {
            return blockComparison;
        }

        int commonLength = Math.Min(Properties.Length, other.Properties.Length);
        for (int index = 0; index < commonLength; index++)
        {
            int keyComparison = Properties[index].Key.CompareTo(other.Properties[index].Key);
            if (keyComparison != 0)
            {
                return keyComparison;
            }

            int valueComparison = StringComparer.Ordinal.Compare(Properties[index].Value, other.Properties[index].Value);
            if (valueComparison != 0)
            {
                return valueComparison;
            }
        }

        return Properties.Length.CompareTo(other.Properties.Length);
    }

    /// <inheritdoc />
    public bool Equals(CanonicalBlockState? other)
    {
        return other is not null && Block.Equals(other.Block) && Properties.SequenceEqual(other.Properties);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as CanonicalBlockState);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Block);
        foreach (BlockStateProperty property in Properties)
        {
            hash.Add(property);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Properties.IsEmpty
            ? Block.ToString()
            : $"{Block}[{string.Join(',', Properties.Select(property => $"{property.Key}={property.Value}"))}]";
    }

    /// <summary>Compares two canonical block states.</summary>
    public static bool operator ==(CanonicalBlockState? left, CanonicalBlockState? right)
    {
        return Equals(left, right);
    }

    /// <summary>Compares two canonical block states.</summary>
    public static bool operator !=(CanonicalBlockState? left, CanonicalBlockState? right)
    {
        return !Equals(left, right);
    }

    /// <summary>Compares two canonical block states in canonical order.</summary>
    public static bool operator <(CanonicalBlockState left, CanonicalBlockState right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Compares two canonical block states in canonical order.</summary>
    public static bool operator <=(CanonicalBlockState left, CanonicalBlockState right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Compares two canonical block states in canonical order.</summary>
    public static bool operator >(CanonicalBlockState left, CanonicalBlockState right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Compares two canonical block states in canonical order.</summary>
    public static bool operator >=(CanonicalBlockState left, CanonicalBlockState right)
    {
        return left.CompareTo(right) >= 0;
    }
}
