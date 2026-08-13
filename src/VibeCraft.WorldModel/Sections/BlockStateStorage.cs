using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;

namespace VibeCraft.WorldModel.Sections;

internal abstract class BlockStateStorage(int count)
{
    internal int Count { get; } = count;

    internal abstract SectionBlockStorageKind Kind { get; }

    internal abstract BlockStateId Get(LocalIndex index);

    internal abstract void CopyTo(Span<BlockStateId> destination);

    internal abstract BlockStateStorage Set(LocalIndex index, BlockStateId state);

    internal abstract SectionStorageMetrics GetMetrics();
}

internal sealed class UniformBlockStateStorage : BlockStateStorage
{
    private readonly BlockStateId _state;

    internal UniformBlockStateStorage(int count, BlockStateId state)
        : base(count)
    {
        _state = state;
    }

    internal override SectionBlockStorageKind Kind => SectionBlockStorageKind.Uniform;

    internal override BlockStateId Get(LocalIndex index)
    {
        ValidateIndex(index);
        return _state;
    }

    internal override void CopyTo(Span<BlockStateId> destination)
    {
        destination[..Count].Fill(_state);
    }

    internal override BlockStateStorage Set(LocalIndex index, BlockStateId state)
    {
        ValidateIndex(index);
        return PalettedBlockStateStorage.FromUniform(Count, _state, index, state);
    }

    internal override SectionStorageMetrics GetMetrics()
    {
        return new SectionStorageMetrics(Kind, Count, 1, 1, 0, 0, 0, 0, sizeof(uint));
    }

    private void ValidateIndex(LocalIndex index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index.Value, (uint)Count, nameof(index));
    }
}

internal sealed class PalettedBlockStateStorage : BlockStateStorage
{
    private readonly int _paletteCount;
    private readonly BlockStateId[] _palette;
    private readonly Dictionary<BlockStateId, byte>? _reverseLookup;
    private readonly PackedPaletteIndices _indices;

    private PalettedBlockStateStorage(
        int count,
        BlockStateId[] palette,
        int paletteCount,
        PackedPaletteIndices indices,
        Dictionary<BlockStateId, byte>? reverseLookup)
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
        BlockStateId initialState,
        LocalIndex changedIndex,
        BlockStateId changedState)
    {
        BlockStateId[] palette = [initialState, changedState];
        PackedPaletteIndices indices = new(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)changedIndex.Value, (uint)count, nameof(changedIndex));
        indices.Set(changedIndex.Value, 1);
        Dictionary<BlockStateId, byte> reverseLookup = new(2)
        {
            [initialState] = 0,
            [changedState] = 1,
        };
        return new PalettedBlockStateStorage(count, palette, 2, indices, reverseLookup);
    }

    internal static PalettedBlockStateStorage FromCanonical(
        ReadOnlySpan<BlockStateId> states,
        BlockStateId[] sortedPalette)
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
        BlockStateId[] palette = new BlockStateId[capacity];
        sortedPalette.CopyTo(palette, 0);
        byte bitsPerEntry = GetBitsPerEntry(sortedPalette.Length);
        PackedPaletteIndices indices = new(states.Length, bitsPerEntry);
        Dictionary<BlockStateId, byte> lookup = new(sortedPalette.Length);
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

    internal override BlockStateId Get(LocalIndex index)
    {
        return _palette[_indices.Get(index.Value)];
    }

    internal override void CopyTo(Span<BlockStateId> destination)
    {
        for (int index = 0; index < Count; index++)
        {
            destination[index] = _palette[_indices.Get(index)];
        }
    }

    internal override BlockStateStorage Set(LocalIndex index, BlockStateId state)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index.Value, (uint)Count, nameof(index));
        if (_reverseLookup is null)
        {
            throw new InvalidOperationException("An immutable snapshot representation cannot be mutated.");
        }

        if (_reverseLookup.TryGetValue(state, out byte paletteIndex))
        {
            _indices.Set(index.Value, paletteIndex);
            return this;
        }

        if (_paletteCount == 256)
        {
            BlockStateId[] direct = new BlockStateId[Count];
            CopyTo(direct);
            direct[index.Value] = state;
            return new DirectBlockStateStorage(direct);
        }

        int nextCount = checked(_paletteCount + 1);
        int nextCapacity = GetPaletteCapacity(nextCount);
        BlockStateId[] nextPalette = new BlockStateId[nextCapacity];
        Array.Copy(_palette, nextPalette, _paletteCount);
        nextPalette[_paletteCount] = state;

        byte nextBits = GetBitsPerEntry(nextCount);
        PackedPaletteIndices nextIndices = nextBits == _indices.BitsPerEntry
            ? _indices.Clone()
            : _indices.Repack(nextBits);
        nextIndices.Set(index.Value, checked((byte)_paletteCount));

        Dictionary<BlockStateId, byte> nextLookup = new(_reverseLookup)
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
    private readonly BlockStateId[] _states;

    internal DirectBlockStateStorage(BlockStateId[] states)
        : base(states?.Length ?? throw new ArgumentNullException(nameof(states)))
    {
        _states = states;
    }

    internal override SectionBlockStorageKind Kind => SectionBlockStorageKind.Direct;

    internal override BlockStateId Get(LocalIndex index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index.Value, (uint)Count, nameof(index));
        return _states[index.Value];
    }

    internal override void CopyTo(Span<BlockStateId> destination)
    {
        _states.CopyTo(destination);
    }

    internal override BlockStateStorage Set(LocalIndex index, BlockStateId state)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index.Value, (uint)Count, nameof(index));
        _states[index.Value] = state;
        return this;
    }

    internal override SectionStorageMetrics GetMetrics()
    {
        return new SectionStorageMetrics(Kind, Count, 0, 0, 32, 0, 0, 1, checked((long)Count * sizeof(uint)));
    }
}
