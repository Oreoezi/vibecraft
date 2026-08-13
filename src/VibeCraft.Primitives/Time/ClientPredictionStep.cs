namespace VibeCraft.Primitives.Time;

/// <summary>
/// Connection-local unsigned 32-bit ordering value for client prediction.
/// </summary>
/// <remarks>
/// Values intentionally wrap. Ordering follows RFC-style serial arithmetic: a distance of
/// <c>2^31</c> is ambiguous and must be rejected or otherwise handled by the caller.
/// </remarks>
public readonly record struct ClientPredictionStep
{
    private const uint HalfRange = 0x8000_0000;

    /// <summary>
    /// Initializes a client prediction step.
    /// </summary>
    /// <param name="value">The connection-local prediction value.</param>
    public ClientPredictionStep(uint value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying serial value.
    /// </summary>
    public uint Value { get; }

    /// <summary>
    /// Gets the prediction step that follows this one, deliberately wrapping at <see cref="uint.MaxValue"/>.
    /// </summary>
    public ClientPredictionStep Next()
    {
        return new ClientPredictionStep(unchecked(Value + 1));
    }

    /// <summary>
    /// Compares this prediction step with <paramref name="other"/> using unsigned 32-bit serial arithmetic.
    /// </summary>
    /// <param name="other">The prediction step to compare against.</param>
    /// <returns>The relative serial order, or <see cref="SerialComparison.Ambiguous"/> at half range.</returns>
    public SerialComparison CompareTo(ClientPredictionStep other)
    {
        uint difference = unchecked(Value - other.Value);
        return difference switch
        {
            0 => SerialComparison.Equal,
            HalfRange => SerialComparison.Ambiguous,
            < HalfRange => SerialComparison.After,
            _ => SerialComparison.Before,
        };
    }
}
