namespace VibeCraft.LogicalCodecs;

/// <summary>
/// Provides the total canonical <c>(kind, dimension, X, Y, Z)</c> order for logical record keys.
/// </summary>
public sealed class LogicalRecordKeyComparer : IComparer<LogicalRecordKey>
{
    private LogicalRecordKeyComparer()
    {
    }

    /// <summary>Gets the shared stateless comparer.</summary>
    public static LogicalRecordKeyComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(LogicalRecordKey left, LogicalRecordKey right)
    {
        int result = ((ushort)left.Kind).CompareTo((ushort)right.Kind);
        if (result != 0)
        {
            return result;
        }

        result = left.Dimension.Value.CompareTo(right.Dimension.Value);
        if (result != 0)
        {
            return result;
        }

        result = left.Coordinate.X.CompareTo(right.Coordinate.X);
        if (result != 0)
        {
            return result;
        }

        result = left.Coordinate.Y.CompareTo(right.Coordinate.Y);
        return result != 0 ? result : left.Coordinate.Z.CompareTo(right.Coordinate.Z);
    }
}
