namespace VibeCraft.WorldModel.Sections;

internal sealed class PackedPaletteIndices
{
    private readonly ulong _mask;
    private readonly ulong[] _words;

    internal PackedPaletteIndices(int count, byte bitsPerEntry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (bitsPerEntry is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), bitsPerEntry, "Palette indices use one through eight bits per entry.");
        }

        Count = count;
        BitsPerEntry = bitsPerEntry;
        _mask = (1UL << bitsPerEntry) - 1UL;
        int bitCount = checked(count * bitsPerEntry);
        _words = new ulong[checked((bitCount + 63) / 64)];
    }

    private PackedPaletteIndices(int count, byte bitsPerEntry, ulong[] words)
    {
        Count = count;
        BitsPerEntry = bitsPerEntry;
        _mask = (1UL << bitsPerEntry) - 1UL;
        _words = words;
    }

    internal byte BitsPerEntry { get; }

    internal int Count { get; }

    internal int WordCount => _words.Length;

    internal byte Get(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        int bitIndex = checked(index * BitsPerEntry);
        int wordIndex = bitIndex >> 6;
        int bitOffset = bitIndex & 63;

        ulong value = _words[wordIndex] >> bitOffset;
        int lowBitCount = 64 - bitOffset;
        if (lowBitCount < BitsPerEntry)
        {
            value |= _words[wordIndex + 1] << lowBitCount;
        }

        return checked((byte)(value & _mask));
    }

    internal void Set(int index, byte value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        if ((value & ~_mask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"The value does not fit in {BitsPerEntry} bits.");
        }

        int bitIndex = checked(index * BitsPerEntry);
        int wordIndex = bitIndex >> 6;
        int bitOffset = bitIndex & 63;
        int lowBitCount = 64 - bitOffset;

        _words[wordIndex] = (_words[wordIndex] & ~(_mask << bitOffset)) | ((ulong)value << bitOffset);
        if (lowBitCount < BitsPerEntry)
        {
            int highBitCount = BitsPerEntry - lowBitCount;
            ulong highMask = (1UL << highBitCount) - 1UL;
            _words[wordIndex + 1] = (_words[wordIndex + 1] & ~highMask) | ((ulong)value >> lowBitCount);
        }
    }

    internal PackedPaletteIndices Repack(byte bitsPerEntry)
    {
        PackedPaletteIndices repacked = new(Count, bitsPerEntry);
        for (int index = 0; index < Count; index++)
        {
            repacked.Set(index, Get(index));
        }

        return repacked;
    }

    internal PackedPaletteIndices Clone()
    {
        return new PackedPaletteIndices(Count, BitsPerEntry, (ulong[])_words.Clone());
    }
}
