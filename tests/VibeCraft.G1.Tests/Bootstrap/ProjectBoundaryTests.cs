using System.Xml.Linq;
using Xunit;

namespace VibeCraft.G1.Tests.Bootstrap;

public sealed class ProjectBoundaryTests
{
    private static readonly string[] AllowedProjectPaths =
    [
        "benchmarks/VibeCraft.G1.Benchmarks/VibeCraft.G1.Benchmarks.csproj",
        "src/VibeCraft.Content/VibeCraft.Content.csproj",
        "src/VibeCraft.LogicalCodecs/VibeCraft.LogicalCodecs.csproj",
        "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
        "src/VibeCraft.Simulation.Abstractions/VibeCraft.Simulation.Abstractions.csproj",
        "src/VibeCraft.WorldModel/VibeCraft.WorldModel.csproj",
        "tests/VibeCraft.G1.Tests/VibeCraft.G1.Tests.csproj",
    ];

    private static readonly Dictionary<string, string[]> ExpectedProjectReferences =
        new(StringComparer.Ordinal)
        {
            ["benchmarks/VibeCraft.G1.Benchmarks/VibeCraft.G1.Benchmarks.csproj"] =
            [
                "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
            ],
            ["src/VibeCraft.Content/VibeCraft.Content.csproj"] =
            [
                "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
            ],
            ["src/VibeCraft.LogicalCodecs/VibeCraft.LogicalCodecs.csproj"] =
            [
                "src/VibeCraft.Content/VibeCraft.Content.csproj",
                "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
                "src/VibeCraft.WorldModel/VibeCraft.WorldModel.csproj",
            ],
            ["src/VibeCraft.Primitives/VibeCraft.Primitives.csproj"] = [],
            ["src/VibeCraft.Simulation.Abstractions/VibeCraft.Simulation.Abstractions.csproj"] =
            [
                "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
                "src/VibeCraft.WorldModel/VibeCraft.WorldModel.csproj",
            ],
            ["src/VibeCraft.WorldModel/VibeCraft.WorldModel.csproj"] =
            [
                "src/VibeCraft.Content/VibeCraft.Content.csproj",
                "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
            ],
            ["tests/VibeCraft.G1.Tests/VibeCraft.G1.Tests.csproj"] =
            [
                "src/VibeCraft.Content/VibeCraft.Content.csproj",
                "src/VibeCraft.LogicalCodecs/VibeCraft.LogicalCodecs.csproj",
                "src/VibeCraft.Primitives/VibeCraft.Primitives.csproj",
                "src/VibeCraft.Simulation.Abstractions/VibeCraft.Simulation.Abstractions.csproj",
                "src/VibeCraft.WorldModel/VibeCraft.WorldModel.csproj",
            ],
        };

    private static readonly Dictionary<string, string[]> ExpectedPackageReferences =
        new(StringComparer.Ordinal)
        {
            ["benchmarks/VibeCraft.G1.Benchmarks/VibeCraft.G1.Benchmarks.csproj"] = ["BenchmarkDotNet"],
            ["src/VibeCraft.Content/VibeCraft.Content.csproj"] = [],
            ["src/VibeCraft.LogicalCodecs/VibeCraft.LogicalCodecs.csproj"] = [],
            ["src/VibeCraft.Primitives/VibeCraft.Primitives.csproj"] = [],
            ["src/VibeCraft.Simulation.Abstractions/VibeCraft.Simulation.Abstractions.csproj"] = [],
            ["src/VibeCraft.WorldModel/VibeCraft.WorldModel.csproj"] = [],
            ["tests/VibeCraft.G1.Tests/VibeCraft.G1.Tests.csproj"] =
            [
                "FsCheck.Xunit",
                "Microsoft.NET.Test.Sdk",
                "coverlet.collector",
                "xunit",
                "xunit.runner.visualstudio",
            ],
        };

    [Fact]
    public void G1SolutionContainsOnlyApprovedProjectsAndDependencies()
    {
        string root = FindRepositoryRoot();
        XDocument solution = XDocument.Load(Path.Combine(root, "VibeCraft.slnx"));
        string[] actualProjectPaths =
        [
            .. solution.Descendants("Project")
                .Select(project => project.Attribute("Path")?.Value
                    ?? throw new InvalidDataException("Every solution project requires a path."))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(AllowedProjectPaths, actualProjectPaths);

        foreach (string relativeProjectPath in actualProjectPaths)
        {
            AssertProjectReferences(root, relativeProjectPath);
            AssertPackageReferences(root, relativeProjectPath);
            AssertLockFileExcludesImplementationDependencies(root, relativeProjectPath);
        }
    }

    private static void AssertProjectReferences(string root, string relativeProjectPath)
    {
        string projectPath = Path.Combine(root, relativeProjectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidDataException($"Project has no parent directory: {relativeProjectPath}");
        XDocument project = XDocument.Load(projectPath);
        string[] actualReferences =
        [
            .. project.Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value
                    ?? throw new InvalidDataException($"ProjectReference in {relativeProjectPath} requires Include."))
                .Select(include => Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(projectDirectory, include)))
                    .Replace('\\', '/'))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(ExpectedProjectReferences[relativeProjectPath], actualReferences);
    }

    private static void AssertPackageReferences(string root, string relativeProjectPath)
    {
        XDocument project = XDocument.Load(Path.Combine(root, relativeProjectPath));
        string[] actualReferences =
        [
            .. project.Descendants("PackageReference")
                .Select(reference => reference.Attribute("Include")?.Value
                    ?? throw new InvalidDataException($"PackageReference in {relativeProjectPath} requires Include."))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(ExpectedPackageReferences[relativeProjectPath], actualReferences);
    }

    private static void AssertLockFileExcludesImplementationDependencies(string root, string relativeProjectPath)
    {
        string projectPath = Path.Combine(root, relativeProjectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidDataException($"Project has no parent directory: {relativeProjectPath}");
        string lockFilePath = Path.Combine(projectDirectory, "packages.lock.json");
        string lockFile = File.ReadAllText(lockFilePath);
        string[] forbiddenPackages = ["Godot", "Microsoft.Data.Sqlite", "Steamworks", "GameNetworkingSockets"];

        foreach (string forbiddenPackage in forbiddenPackages)
        {
            Assert.DoesNotContain(forbiddenPackage, lockFile, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VibeCraft.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the VibeCraft repository root.");
    }
}
