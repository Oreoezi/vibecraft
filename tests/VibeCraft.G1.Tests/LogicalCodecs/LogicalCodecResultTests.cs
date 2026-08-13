using System.Reflection;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using Xunit;

namespace VibeCraft.G1.Tests.LogicalCodecs;

public sealed class LogicalCodecResultTests
{
    [Fact]
    public void FailureAndResultTypesHaveNoPublicOrNonPrivateConstructionOrUsefulDefault()
    {
        Type[] types =
        [
            typeof(LogicalCodecFailure),
            typeof(LogicalEncodeResult),
            typeof(LogicalDecodeResult<LogicalRecordKey>),
            typeof(LogicalDecodeResult<ReferenceProbe>),
        ];

        foreach (Type type in types)
        {
            Assert.True(type.IsSealed);
            Assert.False(type.IsValueType);
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.All(
                type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic),
                constructor => Assert.True(constructor.IsPrivate));
            _ = Assert.Throws<MissingMethodException>(() => Activator.CreateInstance(type));
        }

        LogicalCodecFailure? defaultFailure = default;
        LogicalEncodeResult? defaultEncode = default;
        LogicalDecodeResult<LogicalRecordKey>? defaultStructDecode = default;
        LogicalDecodeResult<ReferenceProbe>? defaultReferenceDecode = default;

        Assert.Null(defaultFailure);
        Assert.Null(defaultEncode);
        Assert.Null(defaultStructDecode);
        Assert.Null(defaultReferenceDecode);
    }

    [Fact]
    public void FailureCodesAndFieldsRetainLegacyValuesAndCoverProjectionFailures()
    {
        Assert.Equal(1, (byte)LogicalCodecFailureCode.IncorrectLength);
        Assert.Equal(2, (byte)LogicalCodecFailureCode.UnknownRecordKind);
        Assert.Equal(3, (byte)LogicalCodecFailureCode.InvalidHeader);
        Assert.Equal(4, (byte)LogicalCodecFailureCode.UnsupportedVersion);
        Assert.Equal(5, (byte)LogicalCodecFailureCode.LimitExceeded);
        Assert.Equal(6, (byte)LogicalCodecFailureCode.ArithmeticOverflow);
        Assert.Equal(7, (byte)LogicalCodecFailureCode.InvalidEnum);
        Assert.Equal(8, (byte)LogicalCodecFailureCode.InvalidText);
        Assert.Equal(9, (byte)LogicalCodecFailureCode.DuplicateIdentity);
        Assert.Equal(10, (byte)LogicalCodecFailureCode.NonCanonicalOrder);
        Assert.Equal(11, (byte)LogicalCodecFailureCode.NonCanonicalPalette);
        Assert.Equal(12, (byte)LogicalCodecFailureCode.IndexOutOfRange);
        Assert.Equal(13, (byte)LogicalCodecFailureCode.UnmappedWorldState);
        Assert.Equal(14, (byte)LogicalCodecFailureCode.TrailingData);
        Assert.Equal(15, (byte)LogicalCodecFailureCode.InvalidValue);

        Assert.Equal(7, (byte)LogicalCodecField.Projection);
        Assert.Equal(8, (byte)LogicalCodecField.Header);
        Assert.Equal(9, (byte)LogicalCodecField.Version);
        Assert.Equal(10, (byte)LogicalCodecField.Mapping);
        Assert.Equal(11, (byte)LogicalCodecField.ContentKey);
        Assert.Equal(12, (byte)LogicalCodecField.Property);
        Assert.Equal(13, (byte)LogicalCodecField.Record);
        Assert.Equal(14, (byte)LogicalCodecField.Side);
        Assert.Equal(15, (byte)LogicalCodecField.Palette);
        Assert.Equal(16, (byte)LogicalCodecField.Voxel);
        Assert.Equal(17, (byte)LogicalCodecField.Sparse);
        Assert.Equal(18, (byte)LogicalCodecField.Payload);
        Assert.Equal(19, (byte)LogicalCodecField.Schedule);
        Assert.Equal(20, (byte)LogicalCodecField.Queue);
        Assert.Equal(21, (byte)LogicalCodecField.DueTick);
        Assert.Equal(22, (byte)LogicalCodecField.Priority);
        Assert.Equal(23, (byte)LogicalCodecField.Sequence);
        Assert.Equal(24, (byte)LogicalCodecField.LocalIndex);
        Assert.Equal(25, (byte)LogicalCodecField.ExpectedType);
        Assert.Equal(26, (byte)LogicalCodecField.Digest);

        AssertEnumValuesAreUnique<LogicalCodecFailureCode>();
        AssertEnumValuesAreUnique<LogicalCodecField>();
    }

    [Fact]
    public void PublicStateIsReadOnlyAndCannotExpressContradictoryBranches()
    {
        AssertReadOnlyProperties(
            typeof(LogicalCodecFailure),
            nameof(LogicalCodecFailure.Code),
            nameof(LogicalCodecFailure.ByteOffset),
            nameof(LogicalCodecFailure.Field),
            nameof(LogicalCodecFailure.RecordIndex),
            nameof(LogicalCodecFailure.ElementIndex));
        AssertReadOnlyProperties(typeof(LogicalEncodeResult), nameof(LogicalEncodeResult.Succeeded), nameof(LogicalEncodeResult.Failure));
        AssertReadOnlyProperties(typeof(LogicalDecodeResult<>), nameof(LogicalDecodeResult<>.Succeeded), nameof(LogicalDecodeResult<>.Value), nameof(LogicalDecodeResult<>.Failure));

        Assert.Empty(typeof(LogicalCodecFailure).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(LogicalEncodeResult).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(LogicalDecodeResult<>).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void FailureFactoriesRejectUndefinedUnknownOrNegativeComponents()
    {
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            LegacyFailureFactoryParameters,
            LogicalCodecFailureCode.Undefined,
            0,
            LogicalCodecField.RecordKey);
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            LegacyFailureFactoryParameters,
            (LogicalCodecFailureCode)byte.MaxValue,
            0,
            LogicalCodecField.RecordKey);
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            LegacyFailureFactoryParameters,
            LogicalCodecFailureCode.IncorrectLength,
            -1,
            LogicalCodecField.RecordKey);
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            LegacyFailureFactoryParameters,
            LogicalCodecFailureCode.IncorrectLength,
            0,
            LogicalCodecField.Undefined);
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            LegacyFailureFactoryParameters,
            LogicalCodecFailureCode.IncorrectLength,
            0,
            (LogicalCodecField)byte.MaxValue);

        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            DetailedFailureFactoryParameters,
            LogicalCodecFailureCode.Undefined,
            0,
            LogicalCodecField.Header,
            -1,
            -1);
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            DetailedFailureFactoryParameters,
            LogicalCodecFailureCode.InvalidHeader,
            0,
            LogicalCodecField.Undefined,
            -1,
            -1);
    }

    [Fact]
    public void LegacyAndDetailedFailureFactoriesProduceValidatedImmutableMetadata()
    {
        LogicalCodecFailure legacy = InvokeFailureFactory(
            LegacyFailureFactoryParameters,
            LogicalCodecFailureCode.IncorrectLength,
            30,
            LogicalCodecField.RecordKey);
        LogicalCodecFailure detailed = InvokeFailureFactory(
            DetailedFailureFactoryParameters,
            LogicalCodecFailureCode.InvalidHeader,
            4,
            LogicalCodecField.Header,
            2,
            7);

        Assert.Equal(LogicalCodecFailureCode.IncorrectLength, legacy.Code);
        Assert.Equal(30, legacy.ByteOffset);
        Assert.Equal(LogicalCodecField.RecordKey, legacy.Field);
        Assert.Equal(-1, legacy.RecordIndex);
        Assert.Equal(-1, legacy.ElementIndex);

        Assert.Equal(LogicalCodecFailureCode.InvalidHeader, detailed.Code);
        Assert.Equal(4, detailed.ByteOffset);
        Assert.Equal(LogicalCodecField.Header, detailed.Field);
        Assert.Equal(2, detailed.RecordIndex);
        Assert.Equal(7, detailed.ElementIndex);
    }

    [Theory]
    [InlineData(-2, -1)]
    [InlineData(-1, -2)]
    public void DetailedFailureFactoryRejectsInvalidOptionalMetadata(int recordIndex, int elementIndex)
    {
        AssertFailureFactoryThrows<ArgumentOutOfRangeException>(
            DetailedFailureFactoryParameters,
            LogicalCodecFailureCode.InvalidHeader,
            0,
            LogicalCodecField.Header,
            recordIndex,
            elementIndex);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void DetailedFailureFactoryAllowsUnavailableMetadataIndependently(int recordIndex, int elementIndex)
    {
        LogicalCodecFailure failure = InvokeFailureFactory(
            DetailedFailureFactoryParameters,
            LogicalCodecFailureCode.InvalidHeader,
            0,
            LogicalCodecField.Header,
            recordIndex,
            elementIndex);

        Assert.Equal(recordIndex, failure.RecordIndex);
        Assert.Equal(elementIndex, failure.ElementIndex);
    }

    [Fact]
    public void StructSuccessHasValueAndNoFailureWhileStructFailureHasFailureAndNoValue()
    {
        LogicalRecordKey expected = new(
            LogicalRecordKind.SectionState,
            new DimensionId(7),
            new SectionCoord(-8, 9, -10));
        byte[] encoded = new byte[LogicalRecordKeyCodecV1.EncodedSize];
        Assert.True(LogicalRecordKeyCodecV1.TryEncode(expected, encoded).Succeeded);

        LogicalDecodeResult<LogicalRecordKey> success = LogicalRecordKeyCodecV1.TryDecode(encoded);
        LogicalDecodeResult<LogicalRecordKey> failure = LogicalRecordKeyCodecV1.TryDecode(encoded.AsSpan(1));

        Assert.True(success.Succeeded);
        Assert.Equal(expected, success.Value);
        Assert.Null(success.Failure);

        Assert.False(failure.Succeeded);
        Assert.NotNull(failure.Failure);
        Assert.NotEqual(LogicalCodecFailureCode.Undefined, failure.Failure.Code);
        Assert.NotEqual(LogicalCodecField.Undefined, failure.Failure.Field);
        Assert.True(failure.Failure.ByteOffset >= 0);
        _ = Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Fact]
    public void ReferenceSuccessHasTheSameNonNullValueAndReferenceFailureHasNoValue()
    {
        ReferenceProbe expected = new("projection");
        LogicalDecodeResult<ReferenceProbe> success = InvokeDecodeFactory<ReferenceProbe>("Success", expected);
        LogicalCodecFailure failureValue = LogicalRecordKeyCodecV1.TryDecode([]).Failure!;
        LogicalDecodeResult<ReferenceProbe> failure = InvokeDecodeFactory<ReferenceProbe>("Failed", failureValue);

        Assert.True(success.Succeeded);
        Assert.Same(expected, success.Value);
        Assert.Null(success.Failure);

        Assert.False(failure.Succeeded);
        Assert.Same(failureValue, failure.Failure);
        _ = Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Fact]
    public void EncodeSuccessAndFailureAreMutuallyExclusive()
    {
        LogicalCodecFailure failureValue = InvokeFailureFactory(
            DetailedFailureFactoryParameters,
            LogicalCodecFailureCode.InvalidValue,
            5,
            LogicalCodecField.Payload,
            0,
            1);
        LogicalEncodeResult success = Assert.IsType<LogicalEncodeResult>(
            GetInternalFactory(typeof(LogicalEncodeResult), "Success").Invoke(null, null));
        LogicalEncodeResult failure = Assert.IsType<LogicalEncodeResult>(
            GetInternalFactory(typeof(LogicalEncodeResult), "Failed").Invoke(null, [failureValue]));

        Assert.True(success.Succeeded);
        Assert.Null(success.Failure);

        Assert.False(failure.Succeeded);
        Assert.Same(failureValue, failure.Failure);
    }

    [Fact]
    public void InternalFactoriesRejectNullReferenceValuesAndFailures()
    {
        AssertFactoryThrows<ArgumentNullException>(typeof(LogicalEncodeResult), "Failed", (object?)null);
        AssertFactoryThrows<ArgumentNullException>(typeof(LogicalDecodeResult<ReferenceProbe>), "Success", (object?)null);
        AssertFactoryThrows<ArgumentNullException>(typeof(LogicalDecodeResult<ReferenceProbe>), "Failed", (object?)null);
    }

    private static readonly Type[] LegacyFailureFactoryParameters =
    [
        typeof(LogicalCodecFailureCode),
        typeof(int),
        typeof(LogicalCodecField),
    ];

    private static readonly Type[] DetailedFailureFactoryParameters =
    [
        typeof(LogicalCodecFailureCode),
        typeof(int),
        typeof(LogicalCodecField),
        typeof(int),
        typeof(int),
    ];

    private static LogicalDecodeResult<T> InvokeDecodeFactory<T>(string name, object argument)
        where T : notnull
    {
        MethodInfo factory = GetInternalFactory(typeof(LogicalDecodeResult<T>), name);
        return Assert.IsType<LogicalDecodeResult<T>>(factory.Invoke(null, [argument]));
    }

    private static LogicalCodecFailure InvokeFailureFactory(Type[] parameterTypes, params object[] arguments)
    {
        MethodInfo factory = GetInternalFactory(typeof(LogicalCodecFailure), "Create", parameterTypes);
        return Assert.IsType<LogicalCodecFailure>(factory.Invoke(null, arguments));
    }

    private static void AssertFailureFactoryThrows<TException>(Type[] parameterTypes, params object?[] arguments)
        where TException : Exception
    {
        MethodInfo factory = GetInternalFactory(typeof(LogicalCodecFailure), "Create", parameterTypes);
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, arguments));
        _ = Assert.IsType<TException>(exception.InnerException);
    }

    private static void AssertFactoryThrows<TException>(Type owner, string name, params object?[] arguments)
        where TException : Exception
    {
        MethodInfo factory = GetInternalFactory(owner, name);
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, arguments));
        _ = Assert.IsType<TException>(exception.InnerException);
    }

    private static MethodInfo GetInternalFactory(Type owner, string name)
    {
        return owner.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SingleOrDefault(method => method.Name == name)
            ?? throw new InvalidOperationException($"Could not locate internal factory {owner.Name}.{name}.");
    }

    private static MethodInfo GetInternalFactory(Type owner, string name, Type[] parameterTypes)
    {
        return owner.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null)
            ?? throw new InvalidOperationException($"Could not locate internal factory {owner.Name}.{name}.");
    }

    private static void AssertReadOnlyProperties(Type owner, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = owner.GetProperty(propertyName)
                ?? throw new InvalidOperationException($"Could not locate property {owner.Name}.{propertyName}.");
            Assert.True(property.CanRead);
            Assert.False(property.CanWrite);
        }
    }

    private static void AssertEnumValuesAreUnique<TEnum>()
        where TEnum : struct, Enum
    {
        TEnum[] values = Enum.GetValues<TEnum>();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    private sealed class ReferenceProbe(string name)
    {
        internal string Name { get; } = name;
    }
}
