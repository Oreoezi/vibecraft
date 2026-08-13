# VibeCraft

VibeCraft is an original, server-authoritative voxel survival sandbox. The repository is currently building the Godot-free G1 core-data laboratory that will freeze coordinate, content-identity, section-state, revision, time, and deterministic-projection contracts before a persistent user world exists.

Start with [docs/READ_ME_FIRST.md](docs/READ_ME_FIRST.md), then read the [requirements baseline](docs/research/PROPOSED-REQUIREMENTS-BASELINE.md), [dependency map](docs/research/DEPENDENCY-MAP.md), and [owner decisions](docs/OWNER_DECISIONS.md).

## Toolchain

- .NET SDK 10.0.400, targeting .NET 10
- Windows x64 and Linux x64
- Godot 4.7.1 .NET is the future client pin; G1 contains no Godot dependency

Build and test with:

```sh
dotnet restore VibeCraft.slnx
dotnet build VibeCraft.slnx --configuration Release --no-restore
dotnet test VibeCraft.slnx --configuration Release --no-build
dotnet run --project benchmarks/VibeCraft.G1.Benchmarks/VibeCraft.G1.Benchmarks.csproj --configuration Release --no-build -- --job Dry --filter '*BootstrapBenchmark*'
```

Implementation work is tracked in [GitHub issues](https://github.com/Oreoezi/vibecraft/issues). Repository agents must follow [AGENTS.md](AGENTS.md).
