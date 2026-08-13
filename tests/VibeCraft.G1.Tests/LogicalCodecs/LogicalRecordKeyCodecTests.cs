using FsCheck.Xunit;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using Xunit;

namespace VibeCraft.G1.Tests.LogicalCodecs;

public sealed class LogicalRecordKeyCodecTests
{
    public static TheoryData<string, uint, long, long, long> GoldenRecordKeys => new()
    {
        {
            "record-key-zero.hex",
            0,
            0,
            0,
            0
        },
        {
            "record-key-mixed-negative.hex",
            0x0123_4567,
            -1,
            1,
            -2
        },
        {
            "record-key-extrema.hex",
            uint.MaxValue,
            long.MinValue,
            long.MaxValue,
            -1
        },
        {
            "record-key-opposite-extrema.hex",
            0x8000_0000,
            long.MaxValue,
            long.MinValue,
            0
        },
    };

    [Fact]
    public void LayoutConstantsDefineExactlyThirtyBytes()
    {
        Assert.Equal(0, LogicalRecordKeyCodecV1.RecordKindOffset);
        Assert.Equal(2, LogicalRecordKeyCodecV1.RecordKindSize);
        Assert.Equal(2, LogicalRecordKeyCodecV1.DimensionOffset);
        Assert.Equal(4, LogicalRecordKeyCodecV1.DimensionSize);
        Assert.Equal(6, LogicalRecordKeyCodecV1.SectionXOffset);
        Assert.Equal(8, LogicalRecordKeyCodecV1.SectionCoordinateSize);
        Assert.Equal(14, LogicalRecordKeyCodecV1.SectionYOffset);
        Assert.Equal(22, LogicalRecordKeyCodecV1.SectionZOffset);
        Assert.Equal(30, LogicalRecordKeyCodecV1.EncodedSize);
    }

    [Theory]
    [MemberData(nameof(GoldenRecordKeys))]
    public void EncodingMatchesHandReviewedLowercaseHexGoldens(
        string fixtureName,
        uint dimension,
        long x,
        long y,
        long z)
    {
        string expectedHex = ReadGolden(fixtureName);
        byte[] destination = new byte[LogicalRecordKeyCodecV1.EncodedSize];

        LogicalEncodeResult result = LogicalRecordKeyCodecV1.TryEncode(Key(dimension, x, y, z), destination);

        Assert.True(result.Succeeded);
        Assert.Null(result.Failure);
        Assert.Equal(LogicalRecordKeyCodecV1.EncodedSize * 2, expectedHex.Length);
        Assert.All(expectedHex, character => Assert.True(
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.Equal(expectedHex, Convert.ToHexStringLower(destination));
    }

    [Theory]
    [MemberData(nameof(GoldenRecordKeys))]
    public void GoldensDecodeAndReencodeCanonically(
        string fixtureName,
        uint dimension,
        long x,
        long y,
        long z)
    {
        byte[] source = Convert.FromHexString(ReadGolden(fixtureName));
        LogicalRecordKey expected = Key(dimension, x, y, z);

        LogicalDecodeResult<LogicalRecordKey> decoded = LogicalRecordKeyCodecV1.TryDecode(source);

        Assert.True(decoded.Succeeded);
        Assert.Equal(expected, decoded.Value);
        Assert.Null(decoded.Failure);

        byte[] reencoded = new byte[LogicalRecordKeyCodecV1.EncodedSize];
        LogicalEncodeResult encoded = LogicalRecordKeyCodecV1.TryEncode(decoded.Value, reencoded);
        Assert.True(encoded.Succeeded);
        Assert.Equal(source, reencoded);
    }

    [Property(MaxTest = 2_000)]
    public bool EveryTypedTupleRoundTrips(uint dimension, long x, long y, long z)
    {
        LogicalRecordKey key = Key(dimension, x, y, z);
        byte[] encoded = new byte[LogicalRecordKeyCodecV1.EncodedSize];

        LogicalEncodeResult encodeResult = LogicalRecordKeyCodecV1.TryEncode(key, encoded);
        LogicalDecodeResult<LogicalRecordKey> decodeResult = LogicalRecordKeyCodecV1.TryDecode(encoded);

        return encodeResult.Succeeded
            && decodeResult.Succeeded
            && decodeResult.Value == key;
    }

    [Property(MaxTest = 2_000)]
    public bool LexicographicBytesHaveExactlyCanonicalTupleOrder(
        uint leftDimension,
        long leftX,
        long leftY,
        long leftZ,
        uint rightDimension,
        long rightX,
        long rightY,
        long rightZ)
    {
        LogicalRecordKey left = Key(leftDimension, leftX, leftY, leftZ);
        LogicalRecordKey right = Key(rightDimension, rightX, rightY, rightZ);
        byte[] leftBytes = Encode(left);
        byte[] rightBytes = Encode(right);

        int tupleOrder = Math.Sign(LogicalRecordKeyComparer.Instance.Compare(left, right));
        int byteOrder = Math.Sign(leftBytes.AsSpan().SequenceCompareTo(rightBytes));

        return tupleOrder == byteOrder;
    }

    [Property(MaxTest = 2_000)]
    public bool EveryAcceptedEncodingIsCanonical(uint dimension, long x, long y, long z)
    {
        byte[] original = Encode(Key(dimension, x, y, z));
        LogicalDecodeResult<LogicalRecordKey> decoded = LogicalRecordKeyCodecV1.TryDecode(original);
        if (!decoded.Succeeded)
        {
            return false;
        }

        byte[] reencoded = Encode(decoded.Value);
        return original.AsSpan().SequenceEqual(reencoded);
    }

    [Fact]
    public void ComparerUsesKindThenDimensionThenXThenYThenZ()
    {
        LogicalRecordKey origin = Key(1, 0, 0, 0);

        AssertPrecedes(new LogicalRecordKey(LogicalRecordKind.Undefined, origin.Section), origin);
        AssertPrecedes(origin, new LogicalRecordKey((LogicalRecordKind)2, origin.Section));
        AssertPrecedes(Key(0, long.MaxValue, long.MaxValue, long.MaxValue), origin);
        AssertPrecedes(Key(1, -1, long.MaxValue, long.MaxValue), origin);
        AssertPrecedes(Key(1, 0, -1, long.MaxValue), origin);
        AssertPrecedes(Key(1, 0, 0, -1), origin);
        Assert.Equal(0, LogicalRecordKeyComparer.Instance.Compare(origin, origin));
    }

    [Theory]
    [InlineData(0, 0, LogicalCodecField.RecordKind)]
    [InlineData(1, 1, LogicalCodecField.RecordKind)]
    [InlineData(2, 2, LogicalCodecField.Dimension)]
    [InlineData(5, 5, LogicalCodecField.Dimension)]
    [InlineData(6, 6, LogicalCodecField.SectionX)]
    [InlineData(13, 13, LogicalCodecField.SectionX)]
    [InlineData(14, 14, LogicalCodecField.SectionY)]
    [InlineData(21, 21, LogicalCodecField.SectionY)]
    [InlineData(22, 22, LogicalCodecField.SectionZ)]
    [InlineData(29, 29, LogicalCodecField.SectionZ)]
    [InlineData(31, 30, LogicalCodecField.RecordKey)]
    public void IncorrectEncodeLengthLeavesDestinationUnchanged(int length, int expectedOffset, LogicalCodecField expectedField)
    {
        byte[] destination = [.. Enumerable.Repeat((byte)0xa5, length)];
        byte[] original = [.. destination];

        LogicalEncodeResult result = LogicalRecordKeyCodecV1.TryEncode(Key(0, 0, 0, 0), destination);

        AssertFailure(result, LogicalCodecFailureCode.IncorrectLength, expectedOffset, expectedField);
        Assert.Equal(original, destination);
    }

    [Theory]
    [InlineData(0, 0, LogicalCodecField.RecordKind)]
    [InlineData(1, 1, LogicalCodecField.RecordKind)]
    [InlineData(2, 2, LogicalCodecField.Dimension)]
    [InlineData(5, 5, LogicalCodecField.Dimension)]
    [InlineData(6, 6, LogicalCodecField.SectionX)]
    [InlineData(13, 13, LogicalCodecField.SectionX)]
    [InlineData(14, 14, LogicalCodecField.SectionY)]
    [InlineData(21, 21, LogicalCodecField.SectionY)]
    [InlineData(22, 22, LogicalCodecField.SectionZ)]
    [InlineData(29, 29, LogicalCodecField.SectionZ)]
    [InlineData(31, 30, LogicalCodecField.RecordKey)]
    public void IncorrectDecodeLengthHasNoPartialValue(int length, int expectedOffset, LogicalCodecField expectedField)
    {
        byte[] source = new byte[length];

        LogicalDecodeResult<LogicalRecordKey> result = LogicalRecordKeyCodecV1.TryDecode(source);

        AssertFailure(result, LogicalCodecFailureCode.IncorrectLength, expectedOffset, expectedField);
        _ = Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Theory]
    [InlineData(LogicalRecordKind.Undefined)]
    [InlineData((LogicalRecordKind)2)]
    [InlineData((LogicalRecordKind)ushort.MaxValue)]
    public void UndefinedOrUnknownEncodeKindLeavesDestinationUnchanged(LogicalRecordKind kind)
    {
        byte[] destination = [.. Enumerable.Repeat((byte)0x5a, LogicalRecordKeyCodecV1.EncodedSize)];
        byte[] original = [.. destination];
        LogicalRecordKey key = new(kind, new DimensionId(1), new SectionCoord(2, 3, 4));

        LogicalEncodeResult result = LogicalRecordKeyCodecV1.TryEncode(key, destination);

        AssertFailure(result, LogicalCodecFailureCode.UnknownRecordKind, 0, LogicalCodecField.RecordKind);
        Assert.Equal(original, destination);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(ushort.MaxValue)]
    public void UndefinedOrUnknownDecodedKindHasNoPartialValue(ushort rawKind)
    {
        byte[] source = Encode(Key(uint.MaxValue, long.MinValue, long.MaxValue, -1));
        source[0] = (byte)(rawKind >> 8);
        source[1] = (byte)rawKind;

        LogicalDecodeResult<LogicalRecordKey> result = LogicalRecordKeyCodecV1.TryDecode(source);

        AssertFailure(result, LogicalCodecFailureCode.UnknownRecordKind, 0, LogicalCodecField.RecordKind);
        _ = Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    private static LogicalRecordKey Key(uint dimension, long x, long y, long z)
    {
        return new LogicalRecordKey(
            LogicalRecordKind.SectionState,
            new DimensionId(dimension),
            new SectionCoord(x, y, z));
    }

    private static byte[] Encode(LogicalRecordKey key)
    {
        byte[] bytes = new byte[LogicalRecordKeyCodecV1.EncodedSize];
        LogicalEncodeResult result = LogicalRecordKeyCodecV1.TryEncode(key, bytes);
        Assert.True(result.Succeeded);
        return bytes;
    }

    private static void AssertPrecedes(LogicalRecordKey left, LogicalRecordKey right)
    {
        Assert.True(LogicalRecordKeyComparer.Instance.Compare(left, right) < 0);
    }

    private static void AssertFailure(
        LogicalEncodeResult result,
        LogicalCodecFailureCode expectedCode,
        int expectedOffset,
        LogicalCodecField expectedField)
    {
        Assert.False(result.Succeeded);
        AssertFailure(result.Failure, expectedCode, expectedOffset, expectedField);
    }

    private static void AssertFailure(
        LogicalDecodeResult<LogicalRecordKey> result,
        LogicalCodecFailureCode expectedCode,
        int expectedOffset,
        LogicalCodecField expectedField)
    {
        Assert.False(result.Succeeded);
        AssertFailure(result.Failure, expectedCode, expectedOffset, expectedField);
    }

    private static void AssertFailure(
        LogicalCodecFailure? failure,
        LogicalCodecFailureCode expectedCode,
        int expectedOffset,
        LogicalCodecField expectedField)
    {
        Assert.NotNull(failure);
        Assert.Equal(expectedCode, failure.Code);
        Assert.Equal(expectedOffset, failure.ByteOffset);
        Assert.Equal(expectedField, failure.Field);
    }

    private static string ReadGolden(string fixtureName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(
                directory.FullName,
                "tests",
                "fixtures",
                "g1",
                "logical-codecs",
                fixtureName);

            if (File.Exists(path))
            {
                return File.ReadAllText(path).Trim();
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate logical-codec fixture '{fixtureName}'.");
    }
}
