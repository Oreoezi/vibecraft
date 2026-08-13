using VibeCraft.Simulation.Abstractions.Phases;
using Xunit;

namespace VibeCraft.G1.Tests.Phases;

public sealed class WorldTickPhaseTests
{
    [Fact]
    public void PhaseOrderIsExplicitAndStable()
    {
        WorldTickPhase[] expected =
        [
            WorldTickPhase.OwnerStart,
            WorldTickPhase.Actions,
            WorldTickPhase.OwnerCommit,
            WorldTickPhase.Publication,
        ];

        Assert.Equal(expected, WorldTickPhaseOrder.InOrder.ToArray());
        Assert.Equal(0, (byte)WorldTickPhase.OwnerStart);
        Assert.Equal(1, (byte)WorldTickPhase.Actions);
        Assert.Equal(2, (byte)WorldTickPhase.OwnerCommit);
        Assert.Equal(3, (byte)WorldTickPhase.Publication);
        Assert.True(WorldTickPhaseOrder.IsBefore(WorldTickPhase.Actions, WorldTickPhase.Publication));
        Assert.False(WorldTickPhaseOrder.IsBefore(WorldTickPhase.Publication, WorldTickPhase.OwnerCommit));
    }

    [Fact]
    public void DefaultPhaseIsIntentionallyTheValidOwnerStartBarrier()
    {
        WorldTickPhase defaultPhase = default;

        Assert.Equal(WorldTickPhase.OwnerStart, defaultPhase);
        Assert.True(WorldTickPhaseOrder.IsBefore(defaultPhase, WorldTickPhase.Actions));
        Assert.False(WorldTickPhaseOrder.IsBefore(defaultPhase, defaultPhase));
    }

    [Fact]
    public void UndefinedPhasesCannotParticipateInOrdering()
    {
        WorldTickPhase undefined = (WorldTickPhase)byte.MaxValue;

        ArgumentOutOfRangeException firstException = Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldTickPhaseOrder.IsBefore(undefined, WorldTickPhase.Publication));
        ArgumentOutOfRangeException secondException = Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldTickPhaseOrder.IsBefore(WorldTickPhase.OwnerStart, undefined));

        Assert.Equal("first", firstException.ParamName);
        Assert.Equal("second", secondException.ParamName);
    }
}
