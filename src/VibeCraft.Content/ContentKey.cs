namespace VibeCraft.Content;

/// <summary>Defines a validated, canonical namespaced content identifier.</summary>
public readonly struct ContentKey : IEquatable<ContentKey>, IComparable<ContentKey>
{
    /// <summary>The largest permitted namespace length.</summary>
    public const int MaxNamespaceLength = 64;

    /// <summary>The largest permitted path length.</summary>
    public const int MaxPathLength = 256;

    private ContentKey(string @namespace, string path)
    {
        Namespace = @namespace;
        Path = path;
    }

    /// <summary>Gets the canonical namespace component.</summary>
    public string Namespace { get; }

    /// <summary>Gets the canonical path component.</summary>
    public string Path { get; }

    /// <summary>Gets whether this value is a validated canonical content key.</summary>
    public bool IsValid => Namespace is not null && Path is not null && IsNamespace(Namespace) && IsPath(Path);

    /// <summary>Creates a key from separately validated canonical components.</summary>
    /// <exception cref="ArgumentException">Thrown when either component is not canonical lowercase ASCII.</exception>
    public static ContentKey Create(string @namespace, string path)
    {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(path);

        return !IsNamespace(@namespace)
            ? throw new ArgumentException("Namespace must match [a-z0-9_.-]+ and be length-bounded.", nameof(@namespace))
            : !IsPath(path)
            ? throw new ArgumentException("Path must match [a-z0-9_./-]+ and be length-bounded.", nameof(path))
            : new ContentKey(@namespace, path);
    }

    /// <summary>Parses one canonical <c>namespace:path</c> key.</summary>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not canonical.</exception>
    public static ContentKey Parse(string value)
    {
        return TryParse(value, out ContentKey key)
            ? key
            : throw new FormatException("Content key must be canonical lowercase ASCII namespace:path.");
    }

    /// <summary>Attempts to parse one canonical <c>namespace:path</c> key.</summary>
    public static bool TryParse(string? value, out ContentKey key)
    {
        key = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        int delimiter = value.IndexOf(':');
        if (delimiter <= 0 || delimiter != value.LastIndexOf(':') || delimiter == value.Length - 1)
        {
            return false;
        }

        string @namespace = value[..delimiter];
        string path = value[(delimiter + 1)..];
        if (!IsNamespace(@namespace) || !IsPath(path))
        {
            return false;
        }

        key = new ContentKey(@namespace, path);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(ContentKey other)
    {
        ThrowIfInvalid();
        other.ThrowIfInvalid();
        int namespaceComparison = StringComparer.Ordinal.Compare(Namespace, other.Namespace);
        return namespaceComparison != 0 ? namespaceComparison : StringComparer.Ordinal.Compare(Path, other.Path);
    }

    /// <inheritdoc />
    public bool Equals(ContentKey other)
    {
        return StringComparer.Ordinal.Equals(Namespace, other.Namespace) && StringComparer.Ordinal.Equals(Path, other.Path);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ContentKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(Namespace ?? string.Empty),
            StringComparer.Ordinal.GetHashCode(Path ?? string.Empty));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsValid ? $"{Namespace}:{Path}" : "<invalid-content-key>";
    }

    /// <summary>Compares two content keys using ordinal semantics.</summary>
    public static bool operator ==(ContentKey left, ContentKey right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two content keys using ordinal semantics.</summary>
    public static bool operator !=(ContentKey left, ContentKey right)
    {
        return !left.Equals(right);
    }

    /// <summary>Compares two content keys using ordinal semantics.</summary>
    public static bool operator <(ContentKey left, ContentKey right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Compares two content keys using ordinal semantics.</summary>
    public static bool operator <=(ContentKey left, ContentKey right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Compares two content keys using ordinal semantics.</summary>
    public static bool operator >(ContentKey left, ContentKey right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Compares two content keys using ordinal semantics.</summary>
    public static bool operator >=(ContentKey left, ContentKey right)
    {
        return left.CompareTo(right) >= 0;
    }

    internal void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("ContentKey is uninitialized or invalid.");
        }
    }

    private static bool IsNamespace(string value)
    {
        return value.Length is > 0 and <= MaxNamespaceLength && value.All(IsNamespaceCharacter);
    }

    private static bool IsPath(string value)
    {
        return value.Length is > 0 and <= MaxPathLength && value.All(IsPathCharacter);
    }

    private static bool IsNamespaceCharacter(char value)
    {
        return value is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.' or '-';
    }

    private static bool IsPathCharacter(char value)
    {
        return IsNamespaceCharacter(value) || value == '/';
    }
}
