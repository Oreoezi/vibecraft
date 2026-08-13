namespace VibeCraft.Primitives.Coordinates;

/// <summary>
/// Defines a dimension's explicit generation and build Y ranges without placing either range in section-key identity.
/// </summary>
public sealed class DimensionRangePolicy
{
    /// <summary>
    /// The exact height, in blocks, of the initial build-range policy.
    /// </summary>
    public const long InitialBuildHeight = 10_000;

    /// <summary>
    /// Initializes a dimension range policy.
    /// </summary>
    /// <param name="generationRange">The finite generation range.</param>
    /// <param name="buildRange">The finite player-build range.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either range is invalid or the generation range is not contained by the build range.</exception>
    public DimensionRangePolicy(BlockYRange generationRange, BlockYRange buildRange)
    {
        if (!generationRange.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(generationRange), generationRange, "A valid, non-empty generation range is required.");
        }

        if (!buildRange.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(buildRange), buildRange, "A valid, non-empty build range is required.");
        }

        if (!buildRange.Contains(generationRange))
        {
            throw new ArgumentOutOfRangeException(nameof(generationRange), generationRange, "The generation range must be contained by the build range.");
        }

        GenerationRange = generationRange;
        BuildRange = buildRange;
    }

    /// <summary>
    /// Gets the finite generation range.
    /// </summary>
    public BlockYRange GenerationRange { get; }

    /// <summary>
    /// Gets the finite player-build range.
    /// </summary>
    public BlockYRange BuildRange { get; }

    /// <summary>
    /// Creates the initial exactly-10,000-block build policy at a caller-selected Y placement.
    /// </summary>
    /// <param name="generationRange">The finite generation range, which must fit inside the resulting build range.</param>
    /// <param name="minBuildY">The inclusive minimum Y value of the resulting build range.</param>
    /// <returns>The initial dimension range policy.</returns>
    /// <exception cref="OverflowException">Thrown when the exclusive maximum for the initial range cannot be represented.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="generationRange"/> is not contained by the resulting build range.</exception>
    public static DimensionRangePolicy CreateInitial(BlockYRange generationRange, long minBuildY)
    {
        return new DimensionRangePolicy(generationRange, new BlockYRange(minBuildY, checked(minBuildY + InitialBuildHeight)));
    }
}
