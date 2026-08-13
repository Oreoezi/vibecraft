using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;

namespace VibeCraft.WorldModel.Sections;

internal enum SectionFixtureKind : byte
{
    UniformAir = 0,
    UniformStone = 1,
    Layered = 2,
    Mixed = 3,
    HighEntropy = 4,
    PaletteBoundary = 5,
}

/// <summary>
/// Defines the deterministic, ephemeral G1 section experiment corpus. It is not world content or a format.
/// </summary>
internal static class SectionCandidateFixture
{
    internal const string FixtureId = "VC-G1-E1-SECTIONS-0.1.0";
    internal const ulong DefaultSeed = 0x5643424654314531UL;

    internal static WorldStateId[] CreateStates(
        SectionGeometry geometry,
        SectionFixtureKind kind,
        ulong seed = DefaultSeed,
        int paletteSize = 0)
    {
        SectionGeometry validatedGeometry = new(geometry.Side);
        int side = validatedGeometry.Side.Value;
        int volume = checked(side * side * side);
        WorldStateId[] states = new WorldStateId[volume];

        switch (kind)
        {
            case SectionFixtureKind.UniformAir:
                break;
            case SectionFixtureKind.UniformStone:
                Array.Fill(states, new WorldStateId(1));
                break;
            case SectionFixtureKind.Layered:
                FillLayered(states, side);
                break;
            case SectionFixtureKind.Mixed:
                FillMixed(states, side, seed);
                break;
            case SectionFixtureKind.HighEntropy:
                for (int index = 0; index < states.Length; index++)
                {
                    states[index] = new WorldStateId(checked((uint)index + 1U));
                }

                break;
            case SectionFixtureKind.PaletteBoundary:
                if (paletteSize is < 1 or > 257)
                {
                    throw new ArgumentOutOfRangeException(nameof(paletteSize), paletteSize, "Palette-boundary fixtures use one through 257 states.");
                }

                for (int index = 0; index < states.Length; index++)
                {
                    states[index] = new WorldStateId(checked((uint)(index % paletteSize) + 1U));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "The section fixture kind is undefined.");
        }

        return states;
    }

    internal static MutableSectionBlockStates CreateSection(
        SectionGeometry geometry,
        ReadOnlySpan<WorldStateId> states,
        SectionRevision revision = default)
    {
        SectionGeometry validatedGeometry = new(geometry.Side);
        int side = validatedGeometry.Side.Value;
        int volume = checked(side * side * side);
        if (states.Length != volume)
        {
            throw new ArgumentException($"The fixture requires exactly {volume} states.", nameof(states));
        }


        int requiredChanges = 0;
        for (int index = 1; index < states.Length; index++)
        {
            if (!states[index].Equals(states[0]))
            {
                requiredChanges = checked(requiredChanges + 1);
            }
        }

        if (revision.Value > long.MaxValue - requiredChanges)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                revision,
                $"Constructing this fixture requires {requiredChanges} actual changes beyond the starting revision.");
        }

        MutableSectionBlockStates section = new(validatedGeometry, states[0], revision);
        for (int index = 1; index < states.Length; index++)
        {
            if (!states[index].Equals(states[0]))
            {
                SectionWriteResult result = section.TrySet(ToLocal(index, side), states[index]);
                if (result != SectionWriteResult.Changed)
                {
                    throw new InvalidOperationException($"Validated fixture construction unexpectedly returned {result} at semantic index {index}.");
                }
            }
        }

        return section;
    }

    internal static WorldStateId[] ExtractSide16(
        ReadOnlySpan<WorldStateId> canonicalSide32Cube,
        int sectionX,
        int sectionY,
        int sectionZ)
    {
        const int largeSide = 32;
        const int smallSide = 16;
        if (canonicalSide32Cube.Length != largeSide * largeSide * largeSide)
        {
            throw new ArgumentException("The equal-volume source must be one canonical 32-cubed fixture.", nameof(canonicalSide32Cube));
        }

        ValidateOctant(sectionX, nameof(sectionX));
        ValidateOctant(sectionY, nameof(sectionY));
        ValidateOctant(sectionZ, nameof(sectionZ));

        WorldStateId[] result = new WorldStateId[smallSide * smallSide * smallSide];
        for (int y = 0; y < smallSide; y++)
        {
            int sourceY = (sectionY * smallSide) + y;
            for (int z = 0; z < smallSide; z++)
            {
                int sourceZ = (sectionZ * smallSide) + z;
                for (int x = 0; x < smallSide; x++)
                {
                    int sourceX = (sectionX * smallSide) + x;
                    int sourceIndex = sourceX + (largeSide * (sourceZ + (largeSide * sourceY)));
                    int destinationIndex = x + (smallSide * (z + (smallSide * y)));
                    result[destinationIndex] = canonicalSide32Cube[sourceIndex];
                }
            }
        }

        return result;
    }

    internal static LocalBlock ToLocal(int index, int side)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, checked((uint)(side * side * side)), nameof(index));
        int x = index % side;
        int z = index / side % side;
        int y = index / checked(side * side);
        return new LocalBlock(x, y, z);
    }

    private static void FillLayered(Span<WorldStateId> states, int side)
    {
        int half = side / 2;
        for (int y = 0; y < side; y++)
        {
            uint state = y < half ? 1U : y == half ? 2U : y == half + 1 ? 3U : 0U;
            states.Slice(y * side * side, side * side).Fill(new WorldStateId(state));
        }
    }

    private static void FillMixed(Span<WorldStateId> states, int side, ulong seed)
    {
        FillLayered(states, side);
        for (int y = 0; y < side; y++)
        {
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    int index = x + (side * (z + (side * y)));
                    ulong cellHash = Mix(seed ^ ((ulong)x * 0x9E3779B185EBCA87UL) ^ ((ulong)y * 0xC2B2AE3D27D4EB4FUL) ^ ((ulong)z * 0x165667B19E3779F9UL));
                    ulong clusterHash = Mix(seed ^ ((ulong)(x >> 2) * 0x94D049BB133111EBUL) ^ ((ulong)(y >> 2) * 0xBF58476D1CE4E5B9UL) ^ ((ulong)(z >> 2) * 0xD6E8FEB86659FD93UL));
                    if ((cellHash & 15UL) == 0UL || clusterHash % 19UL == 0UL)
                    {
                        states[index] = new WorldStateId(checked(4U + (uint)((cellHash ^ clusterHash) % 60UL)));
                    }
                }
            }
        }
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static void ValidateOctant(int value, string parameterName)
    {
        if (value is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A side-16 octant coordinate must be zero or one.");
        }
    }
}
