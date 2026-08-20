using System.Reflection;
using VibeCraft.Content;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.Primitives.Revisions;
using VibeCraft.WorldModel.Sections;
using Xunit;

namespace VibeCraft.G1.Tests.Sections;

public sealed class SectionSemanticSnapshotContractTests
{
    private static readonly (SectionFixtureKind Fixture, SectionBlockStorageKind StorageKind)[] StorageFixtures =
    [
        (SectionFixtureKind.UniformStone, SectionBlockStorageKind.Uniform),
        (SectionFixtureKind.Mixed, SectionBlockStorageKind.Paletted),
        (SectionFixtureKind.HighEntropy, SectionBlockStorageKind.Direct),
    ];

    [Fact]
    public void PublicContractExposesOnlyGeometryRevisionAndSemanticIndexedRead()
    {
        Type contract = typeof(ISectionBlockStateSnapshot);

        Assert.True(contract.IsPublic);
        Assert.True(contract.IsInterface);
        Assert.Empty(contract.GetInterfaces());
        Assert.Empty(contract.GetEvents());
        Assert.Equal(
            [
                "Geometry:Property",
                "GetBlockState:Method",
                "Revision:Property",
                "get_Geometry:Method",
                "get_Revision:Method",
            ],
            contract
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(member => $"{member.Name}:{member.MemberType}")
                .Order(StringComparer.Ordinal));

        PropertyInfo geometry = Assert.Single(contract.GetProperties(), property => property.Name == "Geometry");
        Assert.Equal(typeof(SectionGeometry), geometry.PropertyType);
        Assert.True(geometry.GetMethod?.IsPublic);
        Assert.Null(geometry.SetMethod);
        PropertyInfo revision = Assert.Single(contract.GetProperties(), property => property.Name == "Revision");
        Assert.Equal(typeof(SectionRevision), revision.PropertyType);
        Assert.True(revision.GetMethod?.IsPublic);
        Assert.Null(revision.SetMethod);

        MethodInfo method = Assert.Single(contract.GetMethods(), candidate => !candidate.IsSpecialName);
        Assert.Equal(nameof(ISectionBlockStateSnapshot.GetBlockState), method.Name);
        Assert.Equal(typeof(BlockStateId), method.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal("index", parameter.Name);
        Assert.Equal(typeof(LocalIndex), parameter.ParameterType);

        Assert.True(contract.IsAssignableFrom(typeof(SectionBlockStateSnapshot)));
        Assert.False(contract.IsAssignableFrom(typeof(MutableSectionBlockStates)));
        Assert.False(typeof(SectionBlockStateSnapshot).IsPublic);
        Assert.False(typeof(MutableSectionBlockStates).IsPublic);
        Assert.DoesNotContain(
            contract.GetMembers(),
            member => member.Name.Contains("Storage", StringComparison.Ordinal)
                || member.Name.Contains("Palette", StringComparison.Ordinal)
                || member.Name.Contains("Buffer", StringComparison.Ordinal)
                || member.Name.Contains("Copy", StringComparison.Ordinal));
    }

    [Fact]
    public void ContractReadsUniformPalettedAndDirectSemanticsForBothGeometries()
    {
        foreach (SectionGeometry geometry in new[] { SectionGeometry.Side16, SectionGeometry.Side32 })
        {
            foreach ((SectionFixtureKind fixture, SectionBlockStorageKind storageKind) in StorageFixtures)
            {
                VerifySemanticReads(geometry, fixture, storageKind);
            }
        }
    }

    [Fact]
    public void ContractUsesGeometryForFirstLastAndOutOfRangeIndexes()
    {
        foreach (SectionGeometry geometry in new[] { SectionGeometry.Side16, SectionGeometry.Side32 })
        {
            foreach ((SectionFixtureKind fixture, SectionBlockStorageKind storageKind) in StorageFixtures)
            {
                BlockStateId[] semantic = SectionCandidateFixture.CreateStates(geometry, fixture);
                SectionBlockStateSnapshot concrete = SectionBlockStateSnapshot.Create(
                    geometry,
                    SectionRevision.Initial,
                    semantic);
                Assert.Equal(storageKind, concrete.StorageKind);
                ISectionBlockStateSnapshot snapshot = AsContract(concrete);

                Assert.Equal(semantic[0], snapshot.GetBlockState(default));
                Assert.Equal(semantic[^1], snapshot.GetBlockState(new LocalIndex(geometry.Volume - 1)));
                ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                    () => snapshot.GetBlockState(new LocalIndex(geometry.Volume)));
                Assert.Equal("index", exception.ParamName);
            }
        }
    }

    [Fact]
    public void SnapshotDoesNotAliasCallerInputOrLaterMutableSectionEdits()
    {
        SectionGeometry geometry = SectionGeometry.Side16;
        BlockStateId[] directInput = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.HighEntropy);
        BlockStateId[] directExpected = (BlockStateId[])directInput.Clone();
        ISectionBlockStateSnapshot fromCaller = AsContract(
            SectionBlockStateSnapshot.Create(
                geometry,
                new SectionRevision(17),
                directInput));
        Array.Fill(directInput, default);

        BlockStateId[] semantic = SectionCandidateFixture.CreateStates(geometry, SectionFixtureKind.Mixed);
        MutableSectionBlockStates mutable = SectionCandidateFixture.CreateSection(geometry, semantic);
        ISectionBlockStateSnapshot captured = AsContract(mutable.CaptureSnapshot());
        SectionRevision capturedRevision = captured.Revision;
        for (int value = 1; value <= 300; value++)
        {
            LocalBlock local = geometry.GetLocalBlock(new LocalIndex(value));
            _ = mutable.TrySet(local, new BlockStateId(checked(0x80000000U + (uint)value)));
        }

        Assert.Equal(new SectionRevision(17), fromCaller.Revision);
        AssertSnapshotEquals(directExpected, fromCaller);
        Assert.Equal(capturedRevision, captured.Revision);
        AssertSnapshotEquals(semantic, captured);
    }

    [Fact]
    public async Task EveryRepresentationSupportsConcurrentDeterministicReadsWithoutLocks()
    {
        foreach (SectionGeometry geometry in new[] { SectionGeometry.Side16, SectionGeometry.Side32 })
        {
            foreach ((SectionFixtureKind fixture, SectionBlockStorageKind storageKind) in StorageFixtures)
            {
                BlockStateId[] semantic = SectionCandidateFixture.CreateStates(geometry, fixture);
                SectionBlockStateSnapshot concrete = SectionBlockStateSnapshot.Create(
                    geometry,
                    new SectionRevision(23),
                    semantic);
                Assert.Equal(storageKind, concrete.StorageKind);
                ISectionBlockStateSnapshot snapshot = AsContract(concrete);
                ulong expected = ReadChecksum(snapshot, 0xA0761D6478BD642FUL);

                Task<ulong>[] readers =
                [
                    .. Enumerable.Range(0, 16)
                        .Select(_ => Task.Run(() => ReadChecksum(snapshot, 0xA0761D6478BD642FUL))),
                ];

                ulong[] results = await Task.WhenAll(readers);

                Assert.All(results, result => Assert.Equal(expected, result));
                Assert.Equal(new SectionRevision(23), snapshot.Revision);
            }
        }
    }

    private static void VerifySemanticReads(
        SectionGeometry geometry,
        SectionFixtureKind fixture,
        SectionBlockStorageKind storageKind)
    {
        BlockStateId[] semantic = SectionCandidateFixture.CreateStates(geometry, fixture);
        SectionBlockStateSnapshot concrete = SectionBlockStateSnapshot.Create(
            geometry,
            new SectionRevision(5),
            semantic);
        Assert.Equal(storageKind, concrete.StorageKind);
        ISectionBlockStateSnapshot snapshot = AsContract(concrete);

        Assert.Equal(geometry, snapshot.Geometry);
        Assert.Equal(new SectionRevision(5), snapshot.Revision);
        AssertSnapshotEquals(semantic, snapshot);
    }

    private static void AssertSnapshotEquals(
        ReadOnlySpan<BlockStateId> expected,
        ISectionBlockStateSnapshot snapshot)
    {
        Assert.Equal(expected.Length, snapshot.Geometry.Volume);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], snapshot.GetBlockState(new LocalIndex(index)));
        }
    }

    private static ISectionBlockStateSnapshot AsContract(object candidate)
    {
        Assert.True(candidate is ISectionBlockStateSnapshot);
        return (ISectionBlockStateSnapshot)candidate;
    }

    private static ulong ReadChecksum(ISectionBlockStateSnapshot snapshot, ulong state)
    {
        ulong checksum = 0xCBF29CE484222325UL;
        for (int sample = 0; sample < 32_768; sample++)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong mixed = state;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            LocalIndex index = new(checked((int)(mixed % (uint)snapshot.Geometry.Volume)));
            checksum = unchecked((checksum ^ snapshot.GetBlockState(index).Value) * 0x100000001B3UL);
        }

        return checksum;
    }
}
