using VibeCraft.Content;

namespace VibeCraft.WorldModel.Sections;

internal abstract class BlockStateStorage(int count)
{
    internal int Count { get; } = count;

    internal abstract SectionBlockStorageKind Kind { get; }

    internal abstract WorldStateId Get(int index);

    internal abstract void CopyTo(Span<WorldStateId> destination);

    internal abstract BlockStateStorage Set(int index, WorldStateId state);

    internal abstract SectionStorageMetrics GetMetrics();
}

internal sealed class UniformBlockStateStorage : BlockStateStorage
{
    private readonly WorldStateId _state;

    internal UniformBlockStateStorage(int count, WorldStateId state)
        : base(count)
    {
        _state = state;
    }

    internal override SectionBlockStorageKind Kind => SectionBlockStorageKind.Uniform;

    internal override WorldStateId Get(int index)
    {
        ValidateIndex(index);
        return _state;
    }

    internal override void CopyTo(Span<WorldStateId> destination)
    {
        destination[..Count].Fill(_state);
    }

    internal override BlockStateStorage Set(int index, WorldStateId state)
    {
        ValidateIndex(index);
        return PalettedBlockStateStorage.FromUniform(Count, _state, index, state);
    }

    internal override SectionStorageMetrics GetMetrics()
    {
        return new SectionStorageMetrics(Kind, Count, 1, 1, 0, 0, 0, 0, sizeof(uint));
    }

    private void ValidateIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
    }
}

internal sealed class PalettedBlockStateStorage : BlockStateStorage
{
    private readonly int _paletteCount;
    private readonly WorldStateId[] _palette;
    private readonly Dictionary<WorldStateId, byte>? _reverseLookup;
    private readonly PackedPaletteIndices _indices;

    private PalettedBlockStateStorage(
        int count,
        WorldStateId[] palette,
        int paletteCount,
        PackedPaletteIndices indices,
        Dictionary<WorldStateId, byte>? reverseLookup)
        : base(count)
    {
        _palette = palette;
        _paletteCount = paletteCount;
        _indices = indices;
        _reverseLookup = reverseLookup;
    }

    internal override SectionBlockStorageKind Kind => SectionBlockStorageKind.Paletted;

    internal static PalettedBlockStateStorage FromUniform(
        int count,
        WorldStateId initialState,
        int changedIndex,
        WorldStateId changedState)
    {
        WorldStateId[] palette = [initialState, changedState];
        PackedPaletteIndices indices = new(count, 1);
        indices.Set(changedIndex, 1);
        Dictionary<WorldStateId, byte> reverseLookup = new(2)
        {
            [initialState] = 0,
            [changedState] = 1,
        };
        return new PalettedBlockStateStorage(count, palette, 2, indices, reverseLookup);
    }

    internal static PalettedBlockStateStorage FromCanonical(
        ReadOnlySpan<WorldStateId> states,
        WorldStateId[] sortedPalette)
    {
        if (sortedPalette.Length is < 2 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(sortedPalette));
        }

        for (int index = 1; index < sortedPalette.Length; index++)
        {
            if (sortedPalette[index - 1].Value >= sortedPalette[index].Value)
            {
                throw new ArgumentException("A canonical snapshot palette must be strictly increasing and distinct.", nameof(sortedPalette));
            }
        }

        int capacity = GetPaletteCapacity(sortedPalette.Length);
        WorldStateId[] palette = new WorldStateId[capacity];
        sortedPalette.CopyTo(palette, 0);
        byte bitsPerEntry = GetBitsPerEntry(sortedPalette.Length);
        PackedPaletteIndices indices = new(states.Length, bitsPerEntry);
        Dictionary<WorldStateId, byte> lookup = new(sortedPalette.Length);
        bool[] usedPaletteEntries = new bool[sortedPalette.Length];
        for (int index = 0; index < sortedPalette.Length; index++)
        {
            lookup.Add(sortedPalette[index], checked((byte)index));
        }

        for (int index = 0; index < states.Length; index++)
        {
            if (!lookup.TryGetValue(states[index], out byte paletteIndex))
            {
                throw new ArgumentException("Every semantic state must appear in the canonical snapshot palette.", nameof(states));
            }

            indices.Set(index, paletteIndex);
            usedPaletteEntries[paletteIndex] = true;
        }

        return Array.IndexOf(usedPaletteEntries, false) >= 0
            ? throw new ArgumentException("A canonical snapshot palette cannot contain unused entries.", nameof(sortedPalette))
            : new PalettedBlockStateStorage(states.Length, palette, sortedPalette.Length, indices, null);
    }

    internal override WorldStateId Get(int index)
    {
        return _palette[_indices.Get(index)];
    }

    internal override void CopyTo(Span<WorldStateId> destination)
    {
        for (int index = 0; index < Count; index++)
        {
            destination[index] = _palette[_indices.Get(index)];
        }
    }

    internal override BlockStateStorage Set(int index, WorldStateId state)
    {
        if (_reverseLookup is null)
        {
            throw new InvalidOperationException("An immutable snapshot representation cannot be mutated.");
        }

        if (_reverseLookup.TryGetValue(state, out byte paletteIndex))
        {
            _indices.Set(index, paletteIndex);
            return this;
        }

        if (_paletteCount == 256)
        {
            WorldStateId[] direct = new WorldStateId[Count];
            CopyTo(direct);
            direct[index] = state;
            return new DirectBlockStateStorage(direct);
        }

        int nextCount = checked(_paletteCount + 1);
        int nextCapacity = GetPaletteCapacity(nextCount);
        WorldStateId[] nextPalette = new WorldStateId[nextCapacity];
        Array.Copy(_palette, nextPalette, _paletteCount);
        nextPalette[_paletteCount] = state;

        byte nextBits = GetBitsPerEntry(nextCount);
        PackedPaletteIndices nextIndices = nextBits == _indices.BitsPerEntry
            ? _indices.Clone()
            : _indices.Repack(nextBits);
        nextIndices.Set(index, checked((byte)_paletteCount));

        Dictionary<WorldStateId, byte> nextLookup = new(_reverseLookup)
        {
            [state] = checked((byte)_paletteCount),
        };
        return new PalettedBlockStateStorage(Count, nextPalette, nextCount, nextIndices, nextLookup);
    }

    internal override SectionStorageMetrics GetMetrics()
    {
        long knownPayloadBytes = checked(((long)_palette.Length * sizeof(uint)) + ((long)_indices.WordCount * sizeof(ulong)));
        return new SectionStorageMetrics(
            Kind,
            Count,
            _paletteCount,
            _palette.Length,
            _indices.BitsPerEntry,
            _indices.WordCount,
            _reverseLookup?.Count ?? 0,
            2,
            knownPayloadBytes);
    }

    private static byte GetBitsPerEntry(int paletteCount)
    {
        int bits = 1;
        while ((1 << bits) < paletteCount)
        {
            bits++;
        }

        return checked((byte)bits);
    }

    private static int GetPaletteCapacity(int paletteCount)
    {
        int capacity = 2;
        while (capacity < paletteCount)
        {
            capacity = checked(capacity * 2);
        }

        return capacity;
    }
}

internal sealed class DirectBlockStateStorage : BlockStateStorage
{
    private readonly WorldStateId[] _states;

    internal DirectBlockStateStorage(WorldStateId[] states)
        : base(states?.Length ?? throw new ArgumentNullException(nameof(states)))
    {
        _states = states;
    }

    internal override SectionBlockStorageKind Kind => SectionBlockStorageKind.Direct;

    internal override WorldStateId Get(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        return _states[index];
    }

    internal override void CopyTo(Span<WorldStateId> destination)
    {
        _states.CopyTo(destination);
    }

    internal override BlockStateStorage Set(int index, WorldStateId state)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        _states[index] = state;
        return this;
    }

    internal override SectionStorageMetrics GetMetrics()
    {
        return new SectionStorageMetrics(Kind, Count, 0, 0, 32, 0, 0, 1, checked((long)Count * sizeof(uint)));
    }
}
