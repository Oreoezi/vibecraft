using VibeCraft.Primitives.Time;
using Xunit;

namespace VibeCraft.G1.Tests.Time;

public sealed class ClientSerialTests
{
    [Fact]
    public void InputSequenceWrapsFromMaximumToZero()
    {
        ClientInputSequence sequence = new(uint.MaxValue);

        Assert.Equal(new ClientInputSequence(0), sequence.Next());
        Assert.Equal(SerialComparison.After, new ClientInputSequence(0).CompareTo(sequence));
        Assert.Equal(SerialComparison.Before, sequence.CompareTo(new ClientInputSequence(0)));
    }

    [Fact]
    public void PredictionStepUsesTheSameWrappingSerialOrder()
    {
        ClientPredictionStep step = new(uint.MaxValue);

        Assert.Equal(new ClientPredictionStep(0), step.Next());
        Assert.Equal(SerialComparison.After, new ClientPredictionStep(0).CompareTo(step));
        Assert.Equal(SerialComparison.Before, step.CompareTo(new ClientPredictionStep(0)));
    }

    [Theory]
    [InlineData(0u, 0x8000_0000u)]
    [InlineData(0x8000_0000u, 0u)]
    [InlineData(7u, 0x8000_0007u)]
    public void HalfRangeSerialComparisonsAreExplicitlyAmbiguous(uint left, uint right)
    {
        Assert.Equal(SerialComparison.Ambiguous, new ClientInputSequence(left).CompareTo(new ClientInputSequence(right)));
        Assert.Equal(SerialComparison.Ambiguous, new ClientPredictionStep(left).CompareTo(new ClientPredictionStep(right)));
    }

    [Fact]
    public void EqualSerialValuesAreNotOrdered()
    {
        Assert.Equal(
            SerialComparison.Equal,
            new ClientInputSequence(123).CompareTo(new ClientInputSequence(123)));
        Assert.Equal(
            SerialComparison.Equal,
            new ClientPredictionStep(123).CompareTo(new ClientPredictionStep(123)));
    }
}
