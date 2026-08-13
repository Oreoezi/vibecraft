using System.Reflection;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using Xunit;

namespace VibeCraft.G1.Tests.LogicalCodecs;

public sealed class LogicalCodecResultTests
{
    [Fact]
    public void FailureAndResultTypesHaveNoPublicConstructionOrUsefulDefault()
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
    public void PublicStateIsReadOnlyAndCannotExpressContradictoryBranches()
    {
        AssertReadOnlyProperties(typeof(LogicalCodecFailure), nameof(LogicalCodecFailure.Code), nameof(LogicalCodecFailure.ByteOffset), nameof(LogicalCodecFailure.Field));
        AssertReadOnlyProperties(typeof(LogicalEncodeResult), nameof(LogicalEncodeResult.Succeeded), nameof(LogicalEncodeResult.Failure));
        AssertReadOnlyProperties(typeof(LogicalDecodeResult<>), nameof(LogicalDecodeResult<>.Succeeded), nameof(LogicalDecodeResult<>.Value), nameof(LogicalDecodeResult<>.Failure));

        Assert.Empty(typeof(LogicalCodecFailure).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(LogicalEncodeResult).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(LogicalDecodeResult<>).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void FailureFactoryRejectsUndefinedUnknownOrNegativeComponents()
    {
        AssertFactoryThrows<ArgumentOutOfRangeException>(
            typeof(LogicalCodecFailure),
            "Create",
            LogicalCodecFailureCode.Undefined,
            0,
            LogicalCodecField.RecordKey);
        AssertFactoryThrows<ArgumentOutOfRangeException>(
            typeof(LogicalCodecFailure),
            "Create",
            (LogicalCodecFailureCode)byte.MaxValue,
            0,
            LogicalCodecField.RecordKey);
        AssertFactoryThrows<ArgumentOutOfRangeException>(
            typeof(LogicalCodecFailure),
            "Create",
            LogicalCodecFailureCode.IncorrectLength,
            -1,
            LogicalCodecField.RecordKey);
        AssertFactoryThrows<ArgumentOutOfRangeException>(
            typeof(LogicalCodecFailure),
            "Create",
            LogicalCodecFailureCode.IncorrectLength,
            0,
            LogicalCodecField.Undefined);
        AssertFactoryThrows<ArgumentOutOfRangeException>(
            typeof(LogicalCodecFailure),
            "Create",
            LogicalCodecFailureCode.IncorrectLength,
            0,
            (LogicalCodecField)byte.MaxValue);
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
    public void InternalFactoriesRejectNullReferenceValuesAndFailures()
    {
        AssertFactoryThrows<ArgumentNullException>(typeof(LogicalEncodeResult), "Failed", (object?)null);
        AssertFactoryThrows<ArgumentNullException>(typeof(LogicalDecodeResult<ReferenceProbe>), "Success", (object?)null);
        AssertFactoryThrows<ArgumentNullException>(typeof(LogicalDecodeResult<ReferenceProbe>), "Failed", (object?)null);
    }

    private static LogicalDecodeResult<T> InvokeDecodeFactory<T>(string name, object argument)
        where T : notnull
    {
        MethodInfo factory = GetInternalFactory(typeof(LogicalDecodeResult<T>), name);
        return Assert.IsType<LogicalDecodeResult<T>>(factory.Invoke(null, [argument]));
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
        return owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
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

    private sealed class ReferenceProbe(string name)
    {
        internal string Name { get; } = name;
    }
}
