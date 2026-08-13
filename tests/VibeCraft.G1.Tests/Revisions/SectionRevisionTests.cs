using System.Reflection;
using VibeCraft.Primitives.Revisions;
using VibeCraft.Primitives.Time;
using Xunit;

namespace VibeCraft.G1.Tests.Revisions;

public sealed class SectionRevisionTests
{
    [Fact]
    public void ConstructorRejectsNegativeValues()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(CreateNegativeRevision);

        Assert.NotNull(exception);
    }

    [Fact]
    public void NextAdvancesWithinTheNonnegativeSigned64Domain()
    {
        SectionRevision revision = new(41);

        SectionRevisionAdvanceResult result = revision.TryNext(out SectionRevision next);

        Assert.Equal(SectionRevisionAdvanceResult.Advanced, result);
        Assert.Equal(42, next.Value);
    }

    [Fact]
    public void ComparerUsesSignedNumericOrderAtTheDomainBoundaries()
    {
        SectionRevision initial = SectionRevision.Initial;
        SectionRevision maximum = new(long.MaxValue);

        Assert.Contains(typeof(IComparable<SectionRevision>), typeof(SectionRevision).GetInterfaces());
        Assert.True(initial.CompareTo(new SectionRevision(1)) < 0);
        Assert.True(maximum.CompareTo(SectionRevision.Initial) > 0);
        Assert.Equal(0, maximum.CompareTo(new SectionRevision(long.MaxValue)));
    }

    [Fact]
    public void NextReportsExhaustionWithoutWrapping()
    {
        SectionRevision revision = new(long.MaxValue);

        SectionRevisionAdvanceResult result = revision.TryNext(out SectionRevision next);

        Assert.Equal(SectionRevisionAdvanceResult.Exhausted, result);
        Assert.Equal(revision, next);
    }

    [Fact]
    public void ScalarDomainsDoNotDeclareImplicitNumericOrCrossDomainConversions()
    {
        Type[] scalarDomains =
        [
            typeof(SectionRevision),
            typeof(WorldTick),
            typeof(WorldDuration),
            typeof(ClientInputSequence),
            typeof(ClientPredictionStep),
        ];

        foreach (Type scalarDomain in scalarDomains)
        {
            MethodInfo[] implicitConversions =
            [
                .. scalarDomain.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.Name == "op_Implicit"),
            ];

            Assert.Empty(implicitConversions);
        }
    }

    private static void CreateNegativeRevision()
    {
        SectionRevision revision = new(-1);
        GC.KeepAlive(revision);
    }
}
