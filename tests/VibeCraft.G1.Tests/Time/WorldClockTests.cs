using System.Reflection;
using VibeCraft.Primitives.Time;
using Xunit;

namespace VibeCraft.G1.Tests.Time;

public sealed class WorldClockTests
{
    [Fact]
    public void WorldClockHasExactlySixtyTicksPerRationalSecond()
    {
        WorldDuration duration = new(WorldClock.TicksPerSecond);

        Assert.Equal(60u, WorldClock.TicksPerSecond);
        Assert.Equal(1ul, duration.WholeSeconds);
        Assert.Equal(0u, duration.TicksIntoCurrentSecond);
        Assert.Equal(WorldDurationTimeSpanResult.Exact, duration.TryToTimeSpan(out TimeSpan timeSpan));
        Assert.Equal(TimeSpan.FromSeconds(1), timeSpan);
    }

    [Fact]
    public void ThreeWorldTicksAreExactlyFiftyMillisecondsButOneTickHasARationalRemainder()
    {
        Assert.Equal(
            WorldDurationTimeSpanResult.Exact,
            new WorldDuration(3).TryToTimeSpan(out TimeSpan threeTickDuration));
        Assert.Equal(TimeSpan.FromMilliseconds(50), threeTickDuration);

        WorldDuration oneTick = WorldDuration.OneTick;
        Assert.Equal(0ul, oneTick.WholeSeconds);
        Assert.Equal(1u, oneTick.TicksIntoCurrentSecond);
        Assert.Equal(WorldDurationTimeSpanResult.UnrepresentableRemainder, oneTick.TryToTimeSpan(out _));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ConvertUnrepresentableDuration(oneTick));

        Assert.NotNull(exception);
    }

    [Fact]
    public void TimeSpanToWorldDurationRequiresOrReportsTheChosenRoundingPolicy()
    {
        TimeSpan oneMillisecond = TimeSpan.FromMilliseconds(1);

        Assert.Equal(
            TimeSpanWorldDurationResult.UnrepresentableRemainder,
            WorldClock.TryFromTimeSpan(oneMillisecond, WorldDurationRounding.RequireExact, out WorldDuration rejected));
        Assert.Equal(WorldDuration.Zero, rejected);

        Assert.Equal(
            TimeSpanWorldDurationResult.RoundedDown,
            WorldClock.TryFromTimeSpan(oneMillisecond, WorldDurationRounding.Down, out WorldDuration roundedDown));
        Assert.Equal(WorldDuration.Zero, roundedDown);

        Assert.Equal(
            TimeSpanWorldDurationResult.RoundedUp,
            WorldClock.TryFromTimeSpan(oneMillisecond, WorldDurationRounding.Up, out WorldDuration roundedUp));
        Assert.Equal(WorldDuration.OneTick, roundedUp);
    }

    [Fact]
    public void ExactTimeSpanConversionPreservesTheSixtyTickSecondBoundary()
    {
        Assert.Equal(
            TimeSpanWorldDurationResult.Exact,
            WorldClock.TryFromTimeSpan(TimeSpan.FromSeconds(1), WorldDurationRounding.RequireExact, out WorldDuration duration));
        Assert.Equal(new WorldDuration(60), duration);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    public void UndefinedRoundingIsRejectedBeforeExactOrInexactConversion(long timeSpanTicks)
    {
        WorldDurationRounding undefined = (WorldDurationRounding)byte.MaxValue;

        TimeSpanWorldDurationResult result = WorldClock.TryFromTimeSpan(
            TimeSpan.FromTicks(timeSpanTicks),
            undefined,
            out WorldDuration duration);

        Assert.Equal(TimeSpanWorldDurationResult.InvalidRounding, result);
        Assert.Equal(WorldDuration.Zero, duration);
    }

    [Fact]
    public void TimeSpanBoundsHaveExplicitConversionOutcomes()
    {
        Assert.Equal(
            TimeSpanWorldDurationResult.NegativeInput,
            WorldClock.TryFromTimeSpan(
                TimeSpan.MinValue,
                WorldDurationRounding.RequireExact,
                out WorldDuration negative));
        Assert.Equal(WorldDuration.Zero, negative);

        Assert.Equal(
            TimeSpanWorldDurationResult.InvalidRounding,
            WorldClock.TryFromTimeSpan(
                TimeSpan.MinValue,
                (WorldDurationRounding)byte.MaxValue,
                out WorldDuration invalid));
        Assert.Equal(WorldDuration.Zero, invalid);

        Assert.Equal(
            TimeSpanWorldDurationResult.UnrepresentableRemainder,
            WorldClock.TryFromTimeSpan(
                TimeSpan.MaxValue,
                WorldDurationRounding.RequireExact,
                out WorldDuration maximumRejected));
        Assert.Equal(WorldDuration.Zero, maximumRejected);

        Assert.Equal(
            TimeSpanWorldDurationResult.RoundedDown,
            WorldClock.TryFromTimeSpan(
                TimeSpan.MaxValue,
                WorldDurationRounding.Down,
                out WorldDuration maximumRoundedDown));
        Assert.Equal(
            TimeSpanWorldDurationResult.RoundedUp,
            WorldClock.TryFromTimeSpan(
                TimeSpan.MaxValue,
                WorldDurationRounding.Up,
                out WorldDuration maximumRoundedUp));
        Assert.Equal(checked(maximumRoundedDown.Ticks + 1), maximumRoundedUp.Ticks);
    }

    [Fact]
    public void WorldDurationToTimeSpanDistinguishesExactRemainderAndRangeThresholds()
    {
        const ulong worldTicksPerExactTimeSpanGroup = 3;
        const long timeSpanTicksPerExactGroup = TimeSpan.TicksPerSecond / 20;
        ulong maximumExactGroups = long.MaxValue / timeSpanTicksPerExactGroup;
        ulong maximumExactWorldTicks = checked(maximumExactGroups * worldTicksPerExactTimeSpanGroup);

        WorldDuration maximumExact = new(maximumExactWorldTicks);
        Assert.Equal(
            WorldDurationTimeSpanResult.Exact,
            maximumExact.TryToTimeSpan(out TimeSpan exactTimeSpan));
        Assert.Equal(
            checked((long)maximumExactGroups * timeSpanTicksPerExactGroup),
            exactTimeSpan.Ticks);

        WorldDuration withRemainder = new(checked(maximumExactWorldTicks + 1));
        Assert.Equal(
            WorldDurationTimeSpanResult.UnrepresentableRemainder,
            withRemainder.TryToTimeSpan(out TimeSpan remainderTimeSpan));
        Assert.Equal(TimeSpan.Zero, remainderTimeSpan);

        WorldDuration outOfRange = new(checked(maximumExactWorldTicks + worldTicksPerExactTimeSpanGroup));
        Assert.Equal(
            WorldDurationTimeSpanResult.OutOfRange,
            outOfRange.TryToTimeSpan(out TimeSpan outOfRangeTimeSpan));
        Assert.Equal(TimeSpan.Zero, outOfRangeTimeSpan);
    }

    [Fact]
    public void WorldTickAdvancesOnlyByTheNamedWorldDurationDomain()
    {
        MethodInfo tryAdvance = typeof(WorldTick).GetMethod(nameof(WorldTick.TryAdvance))
            ?? throw new InvalidOperationException("WorldTick.TryAdvance must be present.");

        ParameterInfo[] parameters = tryAdvance.GetParameters();
        Assert.Equal(typeof(WorldDuration), parameters[0].ParameterType);
        Assert.Equal(typeof(WorldTick).MakeByRefType(), parameters[1].ParameterType);

        WorldTick tick = new(ulong.MaxValue - 1);
        Assert.Equal(WorldTickAdvanceResult.Advanced, tick.TryAdvance(WorldDuration.OneTick, out WorldTick maximum));
        Assert.Equal(new WorldTick(ulong.MaxValue), maximum);
        Assert.Equal(WorldTickAdvanceResult.Exhausted, maximum.TryNext(out WorldTick exhausted));
        Assert.Equal(maximum, exhausted);
    }

    [Fact]
    public void WorldTickComparerUsesUnsignedNumericOrderAtTheDomainBoundaries()
    {
        WorldTick initial = WorldTick.Initial;
        WorldTick maximum = new(ulong.MaxValue);

        Assert.Contains(typeof(IComparable<WorldTick>), typeof(WorldTick).GetInterfaces());
        Assert.True(initial.CompareTo(new WorldTick(1)) < 0);
        Assert.True(maximum.CompareTo(WorldTick.Initial) > 0);
        Assert.Equal(0, maximum.CompareTo(new WorldTick(ulong.MaxValue)));
    }

    [Fact]
    public void AuthoritativeProgressionApisDoNotAcceptWallTimeOrRawNumericSubstitutes()
    {
        Type[] authorityTypes =
        [
            typeof(WorldTick),
            typeof(WorldDuration),
        ];
        Type[] forbiddenInputTypes =
        [
            typeof(TimeSpan),
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
        ];

        MethodInfo[] authorityMethods =
        [
            .. authorityTypes.SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)),
        ];
        Assert.NotEmpty(authorityMethods);

        foreach (MethodInfo method in authorityMethods)
        {
            Type[] inputTypes =
            [
                .. method.GetParameters()
                    .Where(parameter => !parameter.IsOut)
                    .Select(parameter => parameter.ParameterType.IsByRef
                        ? parameter.ParameterType.GetElementType()
                            ?? throw new InvalidOperationException("By-reference parameter requires an element type.")
                        : parameter.ParameterType),
            ];

            Assert.DoesNotContain(inputTypes, forbiddenInputTypes.Contains);
        }

        MethodInfo tryAdvance = typeof(WorldTick).GetMethod(nameof(WorldTick.TryAdvance))
            ?? throw new InvalidOperationException("WorldTick.TryAdvance must be present.");
        Assert.Equal(typeof(WorldDuration), tryAdvance.GetParameters()[0].ParameterType);
    }

    private static void ConvertUnrepresentableDuration(WorldDuration duration)
    {
        TimeSpan timeSpan = duration.ToTimeSpanExact();
        GC.KeepAlive(timeSpan);
    }
}
