using System.Reflection;
using ContentAssembly = VibeCraft.Content.AssemblyMarker;
using LogicalCodecsAssembly = VibeCraft.LogicalCodecs.AssemblyMarker;
using PrimitivesAssembly = VibeCraft.Primitives.AssemblyMarker;
using SimulationAbstractionsAssembly = VibeCraft.Simulation.Abstractions.AssemblyMarker;
using WorldModelAssembly = VibeCraft.WorldModel.AssemblyMarker;
using Xunit;

namespace VibeCraft.G1.Tests.Bootstrap;

public sealed class DependencyBoundaryTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(PrimitivesAssembly).Assembly,
        typeof(ContentAssembly).Assembly,
        typeof(WorldModelAssembly).Assembly,
        typeof(SimulationAbstractionsAssembly).Assembly,
        typeof(LogicalCodecsAssembly).Assembly,
    ];

    [Fact]
    public void G1ProductionGraphExcludesEngineStorageAndTransportImplementations()
    {
        string[] forbiddenPrefixes = ["Godot", "Microsoft.Data.Sqlite", "Steamworks", "GameNetworkingSockets"];

        string[] forbiddenReferences =
        [
            .. ProductionAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
            .Where(reference => forbiddenPrefixes.Any(prefix => reference.Contains($"-> {prefix}", StringComparison.Ordinal)))
        ];

        Assert.Empty(forbiddenReferences);
    }
}
