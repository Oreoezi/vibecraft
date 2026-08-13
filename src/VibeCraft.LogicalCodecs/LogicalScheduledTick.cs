using System.Collections.Immutable;
using VibeCraft.Content;
using VibeCraft.Primitives.Time;

namespace VibeCraft.LogicalCodecs;

/// <summary>Identifies the independent section-local queue that owns a scheduled tick.</summary>
public enum LogicalScheduledTickQueueKind : byte
{
    /// <summary>The block scheduled-tick queue.</summary>
    Block = 0,

    /// <summary>The fluid scheduled-tick queue.</summary>
    Fluid = 1,
}

/// <summary>
/// Defines one immutable scheduled-tick semantic value for a G1 logical fixture.
/// </summary>
/// <remarks>
/// This is a storage-neutral fixture value, not a persistence, database, migration, wire, or
/// user-world format. Its sequence is local to this fixture and is not an authority-time domain.
/// </remarks>
public readonly record struct LogicalScheduledTick
{
    /// <summary>The largest number of scheduled ticks admitted for one section fixture.</summary>
    public const int MaxTicksPerSection = 65_536;

    /// <summary>The lowest permitted scheduled-tick priority.</summary>
    public const int MinimumPriority = -3;

    /// <summary>The highest permitted scheduled-tick priority.</summary>
    public const int MaximumPriority = 3;

    /// <summary>
    /// Initializes one validated scheduled semantic tick.
    /// </summary>
    /// <param name="queue">The block or fluid queue.</param>
    /// <param name="dueTick">The absolute logical world tick at which the entry becomes due.</param>
    /// <param name="priority">The bounded priority, where lower values sort first.</param>
    /// <param name="sequence">The fixture-local stable sequence.</param>
    /// <param name="localIndex">The nonnegative section-local index; geometry validates its upper bound.</param>
    /// <param name="expectedType">The canonical content type expected when this tick executes.</param>
    /// <exception cref="ArgumentException">Thrown when the queue or expected content key is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when priority or local index is outside its valid range.</exception>
    public LogicalScheduledTick(
        LogicalScheduledTickQueueKind queue,
        WorldTick dueTick,
        int priority,
        ulong sequence,
        int localIndex,
        ContentKey expectedType)
    {
        if (!Enum.IsDefined(queue))
        {
            throw new ArgumentException("A defined block or fluid scheduled-tick queue is required.", nameof(queue));
        }

        if (priority is < MinimumPriority or > MaximumPriority)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                $"Scheduled-tick priority must be in the range {MinimumPriority} through {MaximumPriority}.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(localIndex);
        if (!expectedType.IsValid)
        {
            throw new ArgumentException("A validated canonical expected content key is required.", nameof(expectedType));
        }

        Queue = queue;
        DueTick = dueTick;
        Priority = priority;
        Sequence = sequence;
        LocalIndex = localIndex;
        ExpectedType = expectedType;
    }

    /// <summary>Gets the independent block or fluid queue.</summary>
    public LogicalScheduledTickQueueKind Queue { get; }

    /// <summary>Gets the absolute world tick at which this entry becomes due.</summary>
    public WorldTick DueTick { get; }

    /// <summary>Gets the bounded priority, where lower values sort first.</summary>
    public int Priority { get; }

    /// <summary>Gets the stable sequence local to this logical fixture.</summary>
    public ulong Sequence { get; }

    /// <summary>Gets the X-contiguous/Z/Y local index of the expected block or fluid.</summary>
    public int LocalIndex { get; }

    /// <summary>Gets the canonical content type expected when this tick executes.</summary>
    public ContentKey ExpectedType { get; }

    internal static ImmutableArray<LogicalScheduledTick> CreateCanonical(
        ImmutableArray<LogicalScheduledTick> inputs,
        int volume)
    {
        HashSet<ulong> sequences = [];
        HashSet<ScheduledTickCoalescingIdentity> coalescingIdentities = [];
        List<LogicalScheduledTick> ticks = [];
        foreach (LogicalScheduledTick tick in inputs)
        {
            tick.ThrowIfInvalid();
            ValidateLocalIndex(tick.LocalIndex, volume, nameof(inputs));
            if (!sequences.Add(tick.Sequence))
            {
                throw new ArgumentException(
                    $"Scheduled-tick sequence {tick.Sequence} is supplied more than once.",
                    nameof(inputs));
            }

            ScheduledTickCoalescingIdentity identity = new(tick.Queue, tick.LocalIndex, tick.ExpectedType);
            if (!coalescingIdentities.Add(identity))
            {
                throw new ArgumentException(
                    "Scheduled ticks cannot duplicate a queue, local-index, and expected-type coalescing identity.",
                    nameof(inputs));
            }

            ticks.Add(tick);
        }

        return
        [
            .. ticks
                .OrderBy(tick => tick.Queue)
                .ThenBy(tick => tick.DueTick.Value)
                .ThenBy(tick => tick.Priority)
                .ThenBy(tick => tick.Sequence),
        ];
    }

    internal void ThrowIfInvalid()
    {
        if (!Enum.IsDefined(Queue) ||
            Priority is < MinimumPriority or > MaximumPriority ||
            LocalIndex < 0 ||
            !ExpectedType.IsValid)
        {
            throw new InvalidOperationException("LogicalScheduledTick is uninitialized or invalid.");
        }
    }

    private static void ValidateLocalIndex(int localIndex, int volume, string parameterName)
    {
        if (localIndex >= volume)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                localIndex,
                $"A scheduled-tick local index must be in the range 0 through {volume - 1}.");
        }
    }

    private readonly record struct ScheduledTickCoalescingIdentity(
        LogicalScheduledTickQueueKind Queue,
        int LocalIndex,
        ContentKey ExpectedType);
}
