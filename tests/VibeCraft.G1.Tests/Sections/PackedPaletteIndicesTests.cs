using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.Sections;

public sealed class PackedPaletteIndicesTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void ValuesCrossingUlongBoundariesRoundTrip(int bitsPerEntry)
    {
        const int count = 521;
        PackedPaletteIndices indices = new(count, checked((byte)bitsPerEntry));
        int mask = (1 << bitsPerEntry) - 1;
        byte[] expectedValues = new byte[count];

        for (int index = 0; index < count; index++)
        {
            int pattern = (index * 29) + 7;
            byte expected = checked((byte)(pattern & mask));
            expectedValues[index] = expected;
            indices.Set(index, expected);
        }

        for (int index = 0; index < count; index++)
        {
            int pattern = (index * 29) + 7;
            byte expected = checked((byte)(pattern & mask));
            Assert.Equal(expected, indices.Get(index));
        }

        for (int bitBoundary = 64; bitBoundary < count * bitsPerEntry; bitBoundary += 64)
        {
            int crossingIndex = bitBoundary / bitsPerEntry;
            if (crossingIndex * bitsPerEntry < bitBoundary && (crossingIndex + 1) * bitsPerEntry > bitBoundary)
            {
                byte replacement = checked((byte)(mask - indices.Get(crossingIndex)));
                expectedValues[crossingIndex] = replacement;
                indices.Set(crossingIndex, replacement);
                for (int index = 0; index < count; index++)
                {
                    Assert.Equal(expectedValues[index], indices.Get(index));
                }
            }
        }
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(32768)]
    public void RandomizedRepeatedWritesNeverCorruptNeighbors(int count)
    {
        for (byte bitsPerEntry = 1; bitsPerEntry <= 8; bitsPerEntry++)
        {
            PackedPaletteIndices indices = new(count, bitsPerEntry);
            byte[] expected = new byte[count];
            ulong random = 0xE7037ED1A0B428DBUL ^ bitsPerEntry ^ checked((ulong)count);
            int mask = (1 << bitsPerEntry) - 1;
            int writeCount = checked(count * 2);
            for (int write = 0; write < writeCount; write++)
            {
                int index = checked((int)(Next(ref random) % (uint)count));
                byte value = checked((byte)(Next(ref random) & (uint)mask));
                expected[index] = value;
                indices.Set(index, value);
            }

            for (int index = 0; index < count; index++)
            {
                Assert.Equal(expected[index], indices.Get(index));
            }
        }
    }

    [Fact]
    public void RepackPreservesEveryValue()
    {
        PackedPaletteIndices original = new(4096, 3);
        for (int index = 0; index < original.Count; index++)
        {
            original.Set(index, checked((byte)(index & 7)));
        }

        PackedPaletteIndices repacked = original.Repack(7);

        Assert.Equal(7, repacked.BitsPerEntry);
        for (int index = 0; index < original.Count; index++)
        {
            Assert.Equal(original.Get(index), repacked.Get(index));
        }
    }

    [Fact]
    public void InvalidPackedAccessIsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PackedPaletteIndices(0, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PackedPaletteIndices(1, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PackedPaletteIndices(1, 9));

        PackedPaletteIndices indices = new(3, 2);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => indices.Get(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => indices.Get(3));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => indices.Set(0, 4));
    }

    private static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
