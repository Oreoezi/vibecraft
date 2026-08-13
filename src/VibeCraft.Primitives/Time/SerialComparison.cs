namespace VibeCraft.Primitives.Time;

/// <summary>
/// Relative ordering under unsigned 32-bit serial arithmetic.
/// </summary>
public enum SerialComparison : byte
{
    /// <summary>
    /// Both serial values are equal.
    /// </summary>
    Equal = 0,

    /// <summary>
    /// The left serial value is before the right serial value.
    /// </summary>
    Before = 1,

    /// <summary>
    /// The left serial value is after the right serial value.
    /// </summary>
    After = 2,

    /// <summary>
    /// The values are exactly half the unsigned 32-bit range apart and have no defined order.
    /// </summary>
    Ambiguous = 3,
}
