using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VibeCraft.Content;
using VibeCraft.LogicalCodecs;
using VibeCraft.Primitives.Coordinates;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

/// <summary>
/// Runs the predeclared G1/E1 equal-world-volume observation protocol.
/// </summary>
/// <remarks>
/// This runner deliberately reports fixture observations rather than a save, network, durable-state,
/// or user-world format. G0 owner acceptance is absent, so a completed run is always recorded as
/// <c>defer</c>; no section-side, indexing, or compatibility constant is frozen here.
/// </remarks>
internal static class E1CoreDataReport
{
    internal const string G0FixtureId = "VC-G0-FP-0.1.0";
    internal const string SectionFixtureId = "VC-G1-E1-SECTIONS-0.1.0";
    internal const string ProjectionFixtureId = "VC-G1-E1-LOGICAL-PROJECTION-0.1.0";
    internal const string SemanticFingerprintDomain = "VC-G1-E1-SEMANTIC-FP-0.1.0";

    private const ulong Seed = 0x5643424654314531UL;
    private const ulong CubeSeedStride = 0x9E3779B97F4A7C15UL;
    private const int CubeSide = SectionEqualVolumeFixture.CubeSide;
    private const int CubeVolume = SectionEqualVolumeFixture.CubeVolume;
    private const int DenseValueBytes = sizeof(uint);
    private const long MaxRawObservationBytes = 512L * 1024 * 1024;
    private const string StableRuntimeMarker = "VIBECRAFT_E1_STABLE_RUNTIME";
    private const long MaxRequestedBytes = 64L * 1024 * 1024 * 1024;
    private static readonly TimeSpan CiMemoryChildTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FullMemoryChildTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CiMemoryPhaseTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FullMemoryPhaseTimeout = TimeSpan.FromHours(4);
    private static readonly int[] PaletteBoundaries = [1, 2, 3, 4, 5, 8, 9, 16, 17, 32, 33, 64, 65, 128, 129, 256, 257];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Runs one bounded E1 observation profile and returns zero for every completed disposition.</summary>
    internal static int Run(string[] args)
    {
        try
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(StableRuntimeMarker), "1", StringComparison.Ordinal))
            {
                return RelaunchWithStableRuntime(args);
            }

            E1Options options = E1Options.Parse(args);
            ValidateFixtureIdentity();
            ValidateIndexContract();
            WarmReadMeasurementPaths();

            SectionObservationManifest manifest = SectionObservationManifest.Capture(
                Seed,
                Environment.CommandLine,
                $"E1 {options.Profile} observational protocol; corpusCubes={options.CubeCount}; performanceCubes={options.PerformanceCubeCount}; rounds={options.PairedRounds}; clustersPerTrace={options.TotalClustersPerTrace}");
            using RawObservationSink raw = new(options.OutputDirectory);
            E1RunAccumulator accumulator = new(options, raw);
            accumulator.RecordPaletteBoundaries();
            accumulator.RunCorpus();
            E1MemoryReport memory = RunFreshProcessMemory(options);
            E1ReportDocument document = accumulator.CreateDocument(manifest, memory);
            WriteReport(document, raw, options.OutputDirectory);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or IOException or JsonException or OverflowException or TimeoutException)
        {
            Console.Error.WriteLine($"E1 core-data report failed: {exception.Message}");
            return 1;
        }
    }

    private static int RelaunchWithStableRuntime(string[] args)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The benchmark executable path is unavailable for the fixed-runtime E1 child.");
        ProcessStartInfo startInfo = new(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(E1CoreDataReport).Assembly.Location);
        }

        startInfo.ArgumentList.Add("--e1-report");
        foreach (string argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[StableRuntimeMarker] = "1";
        startInfo.Environment["DOTNET_TieredCompilation"] = "0";
        startInfo.Environment["DOTNET_TieredPGO"] = "0";
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The fixed-runtime E1 child process could not be started.");
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// Runs one retained-memory child process. Program dispatch owns the <c>--e1-memory-child</c> switch.
    /// </summary>
    internal static int RunMemoryChild(string[] args)
    {
        try
        {
            E1MemoryChildOptions options = E1MemoryChildOptions.Parse(args);
            ValidateFixtureIdentity();
            int selectedCubeCount = CountMemoryDistribution(options.CubeCount, options.Distribution);
            long estimatedDenseBytes = checked((long)selectedCubeCount * CubeVolume * DenseValueBytes);
            if (estimatedDenseBytes > options.MaxBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(args),
                    $"The requested corpus needs at least {estimatedDenseBytes} dense semantic bytes, above --max-bytes {options.MaxBytes}.");
            }

            WarmMemoryMode(options.Mode, options.Distribution);
            CollectForRetainedMemory();
            long before = GC.GetTotalMemory(forceFullCollection: false);
            RetainedCorpus corpus = BuildRetainedCorpus(options.Mode, options.CubeCount, options.Distribution);
            CollectForRetainedMemory();
            long after = GC.GetTotalMemory(forceFullCollection: false);
            long retained = checked(after - before);
            E1MemoryTrial trial = new(
                options.Mode,
                options.Distribution,
                options.TrialIndex,
                options.LaunchOrder,
                Environment.ProcessId,
                options.CubeCount,
                selectedCubeCount,
                options.MaxBytes,
                corpus.KnownPayloadBytes,
                corpus.SemanticChecksum.ToString("x16", CultureInfo.InvariantCulture),
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                GCSettings.IsServerGC,
                GCSettings.LatencyMode.ToString(),
                before,
                after,
                retained,
                retained >= 0,
                retained >= 0 ? null : "GC returned a negative retained delta; this raw trial is inconclusive and was not clamped.");
            GC.KeepAlive(corpus.Roots);
            Console.WriteLine(JsonSerializer.Serialize(trial, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or OverflowException)
        {
            Console.Error.WriteLine($"E1 retained-memory child failed: {exception.Message}");
            return 1;
        }
    }

    private static void ValidateFixtureIdentity()
    {
        if (!string.Equals(SectionCandidateFixture.FixtureId, SectionFixtureId, StringComparison.Ordinal) ||
            SectionCandidateFixture.DefaultSeed != Seed ||
            !string.Equals(CanonicalLogicalProjectionCodecV1.FixtureId, ProjectionFixtureId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The E1 report does not match the predeclared G0, section, or logical-projection fixture identity.");
        }
    }

    private static void ValidateIndexContract()
    {
        foreach (SectionGeometry geometry in new[] { SectionGeometry.Side16, SectionGeometry.Side32 })
        {
            int side = geometry.Side.Value;
            int volume = checked(side * side * side);
            bool[] seen = new bool[volume];
            int expected = 0;
            for (int y = 0; y < side; y++)
            {
                for (int z = 0; z < side; z++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        int actual = geometry.GetLinearIndex(geometry.CreateLocal(x, y, z));
                        if (actual != expected || actual < 0 || actual >= volume || seen[actual])
                        {
                            throw new InvalidOperationException($"The X-to-Z-to-Y local-index contract is not bijective for side {side} at ({x}, {y}, {z}).");
                        }

                        seen[actual] = true;
                        expected++;
                    }
                }
            }

            if (seen.Any(value => !value))
            {
                throw new InvalidOperationException($"The X-to-Z-to-Y local-index contract leaves a hole for side {side}.");
            }
        }
    }

    private static void WarmReadMeasurementPaths()
    {
        foreach (SectionFixtureKind distribution in new[]
        {
            SectionFixtureKind.UniformAir,
            SectionFixtureKind.Mixed,
            SectionFixtureKind.HighEntropy,
        })
        {
            WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(distribution, Seed);
            int[] randomTrace = SectionBenchmarkSupport.CreateRandomTrace(
                SectionBenchmarkSupport.RandomTraceLength,
                CubeVolume,
                Seed ^ (ulong)distribution);
            MutableSectionBlockStates[] one32 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.OneSide32, canonical);
            MutableSectionBlockStates[] eight16 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, canonical);
            for (int iteration = 0; iteration < 64; iteration++)
            {
                _ = ReadDenseRandom(canonical, randomTrace);
                _ = ReadDenseLinear(canonical);
                _ = ReadRandom(one32, SectionEqualVolumeLayout.OneSide32, randomTrace);
                _ = ReadLinear(one32, SectionEqualVolumeLayout.OneSide32);
                _ = ReadRandom(eight16, SectionEqualVolumeLayout.EightSide16, randomTrace);
                _ = ReadLinear(eight16, SectionEqualVolumeLayout.EightSide16);
            }
        }

        Thread.Sleep(TimeSpan.FromMilliseconds(100));
    }

    private static E1MemoryReport RunFreshProcessMemory(E1Options options)
    {
        try
        {
            List<E1MemoryTrial> trials = [];
            int launchOrder = 0;
            Stopwatch phaseClock = Stopwatch.StartNew();
            TimeSpan phaseTimeout = options.Profile == E1Profile.Ci ? CiMemoryPhaseTimeout : FullMemoryPhaseTimeout;
            for (int trialIndex = 0; trialIndex < options.MemoryTrials; trialIndex++)
            {
                foreach (E1MemoryDistribution distribution in Enum.GetValues<E1MemoryDistribution>())
                {
                    E1MemoryMode[] modes = Enum.GetValues<E1MemoryMode>();
                    int rotation = (trialIndex + (int)distribution) % modes.Length;
                    for (int position = 0; position < modes.Length; position++)
                    {
                        E1MemoryMode mode = modes[(position + rotation) % modes.Length];
                        try
                        {
                            TimeSpan remaining = phaseTimeout - phaseClock.Elapsed;
                            if (remaining <= TimeSpan.Zero)
                            {
                                throw new TimeoutException($"The fixed {phaseTimeout} retained-memory phase deadline elapsed.");
                            }

                            TimeSpan childTimeout = options.Profile == E1Profile.Ci ? CiMemoryChildTimeout : FullMemoryChildTimeout;
                            trials.Add(LaunchMemoryTrial(
                                options,
                                mode,
                                distribution,
                                trialIndex,
                                launchOrder,
                                remaining < childTimeout ? remaining : childTimeout));
                        }
                        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException or JsonException)
                        {
                            trials.Add(new E1MemoryTrial(
                                mode,
                                distribution,
                                trialIndex,
                                launchOrder,
                                0,
                                options.CubeCount,
                                CountMemoryDistribution(options.CubeCount, distribution),
                                options.MaxBytes,
                                0,
                                string.Empty,
                                RuntimeInformation.FrameworkDescription,
                                RuntimeInformation.ProcessArchitecture.ToString(),
                                GCSettings.IsServerGC,
                                GCSettings.LatencyMode.ToString(),
                                0,
                                0,
                                0,
                                false,
                                exception.Message));
                        }

                        launchOrder++;
                    }
                }
            }

            E1MemoryModeSummary[] summaries =
            [
                .. from mode in Enum.GetValues<E1MemoryMode>()
                   from distribution in Enum.GetValues<E1MemoryDistribution>()
                   select SummarizeMemoryMode(
                       mode,
                       distribution,
                       [.. trials.Where(trial => trial.Mode == mode && trial.Distribution == distribution)]),
            ];
            bool allConclusive = summaries.All(summary => summary.IsConclusive);
            return new E1MemoryReport(
                allConclusive,
                allConclusive ? null : "At least one fresh-process mode/distribution trial failed or was invalid; completed and failed attempts are retained below.",
                $"Each mode/distribution/trial is a fresh process with a retained root and explicit full compacting collections before and after allocation. Mode launch order rotates deterministically within trial/distribution, and the entire memory phase has a fixed {phaseTimeout} deadline. The fixed corpus contributes equal counts of homogeneous, layered, mixed, and high-entropy cubes. GC deltas are not storage, save, network, or wire bytes.",
                summaries,
                [.. trials]);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException or JsonException)
        {
            return new E1MemoryReport(
                false,
                exception.Message,
                "Fresh-process retained memory was not available for this invocation. This is an explicit inconclusive observation, not a substituted in-process estimate.",
                [],
                []);
        }
    }

    private static E1MemoryTrial LaunchMemoryTrial(
        E1Options options,
        E1MemoryMode mode,
        E1MemoryDistribution distribution,
        int trialIndex,
        int launchOrder,
        TimeSpan timeout)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current benchmark executable path is unavailable for a fresh-process memory trial.");
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(E1CoreDataReport).Assembly.Location);
        }

        startInfo.ArgumentList.Add("--e1-memory-child");
        AddArgument(startInfo, "--profile", options.Profile.ToString().ToLowerInvariant());
        AddArgument(startInfo, "--mode", mode.ToString());
        AddArgument(startInfo, "--distribution", distribution.ToString());
        AddArgument(startInfo, "--trial-index", trialIndex.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--launch-order", launchOrder.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--cubes", options.CubeCount.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--max-bytes", options.MaxBytes.ToString(CultureInfo.InvariantCulture));
        ProcessExecutionResult execution = SectionBenchmarkSupport.RunProcess(
            startInfo,
            timeout);
        if (execution.TimedOut || execution.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Fresh-process trial {mode}/{distribution}/{trialIndex} did not complete (exit={execution.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, timedOut={execution.TimedOut}). stderr={TrimForError(execution.StandardError)}");
        }

        E1MemoryTrial? trial = JsonSerializer.Deserialize<E1MemoryTrial>(execution.StandardOutput, JsonOptions);
        return trial ?? throw new InvalidDataException($"Fresh-process trial {mode}/{trialIndex} produced no structured JSON.");
    }

    private static E1MemoryModeSummary SummarizeMemoryMode(
        E1MemoryMode mode,
        E1MemoryDistribution distribution,
        E1MemoryTrial[] trials)
    {
        long[] valid = [.. trials.Where(trial => trial.IsValid).Select(trial => trial.RetainedBytes).Order()];
        return valid.Length == 0
            ? new E1MemoryModeSummary(mode, distribution, 0, trials.Length, false, null, null, null, null)
            : new E1MemoryModeSummary(
                mode,
                distribution,
                valid.Length,
                trials.Length - valid.Length,
                valid.Length == trials.Length,
                valid[0],
                Median(valid),
                valid[^1],
                Median([.. valid.Select(value => Math.Abs(value - Median(valid))).Order()]));
    }

    private static RetainedCorpus BuildRetainedCorpus(
        E1MemoryMode mode,
        int cubeCount,
        E1MemoryDistribution selectedDistribution)
    {
        object[] roots = new object[CountMemoryDistribution(cubeCount, selectedDistribution)];
        long knownPayloadBytes = 0;
        ulong checksum = 0xCBF29CE484222325UL;
        int rootIndex = 0;
        for (int ordinal = 0; ordinal < cubeCount; ordinal++)
        {
            if (MemoryDistributionForOrdinal(ordinal) != selectedDistribution)
            {
                continue;
            }

            SectionFixtureKind distribution = DistributionForOrdinal(ordinal);
            WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(distribution, CubeSeedFor(ordinal));
            checksum = unchecked((checksum * 31UL) ^ SectionBenchmarkSupport.Checksum(canonical));
            if (mode == E1MemoryMode.DenseCanonical)
            {
                roots[rootIndex++] = canonical;
                knownPayloadBytes = checked(knownPayloadBytes + ((long)canonical.Length * DenseValueBytes));
                continue;
            }

            SectionEqualVolumeLayout layout = mode == E1MemoryMode.OneSide32Adaptive
                ? SectionEqualVolumeLayout.OneSide32
                : SectionEqualVolumeLayout.EightSide16;
            MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
            SectionEqualVolumeFixture.ValidateSections(sections, layout);
            roots[rootIndex++] = sections;
            foreach (MutableSectionBlockStates section in sections)
            {
                knownPayloadBytes = checked(knownPayloadBytes + section.GetStorageMetrics().KnownPayloadBytes);
            }
        }

        return new RetainedCorpus(roots, knownPayloadBytes, checksum);
    }

    private static void WarmMemoryMode(E1MemoryMode mode, E1MemoryDistribution distribution)
    {
        int warmCubeCount = distribution switch
        {
            E1MemoryDistribution.Homogeneous => 1,
            E1MemoryDistribution.Layered => 2,
            E1MemoryDistribution.Mixed => 3,
            E1MemoryDistribution.HighEntropy => 4,
            _ => throw new InvalidOperationException("The E1 retained-memory distribution is incomplete."),
        };
        RetainedCorpus warm = BuildRetainedCorpus(mode, warmCubeCount, distribution);
        GC.KeepAlive(warm.Roots);
    }

    private static void CollectForRetainedMemory()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static string TrimForError(string text)
    {
        const int maximumLength = 2_048;
        string value = string.IsNullOrWhiteSpace(text) ? "<empty>" : text.Trim();
        return value.Length <= maximumLength ? value : $"{value[..maximumLength]}… <truncated>";
    }

    private static ulong CubeSeedFor(int ordinal)
    {
        return unchecked(Seed + ((ulong)ordinal * CubeSeedStride));
    }

    private static SectionFixtureKind DistributionForOrdinal(int ordinal)
    {
        return (ordinal % 4) switch
        {
            0 => ordinal / 4 % 2 == 0 ? SectionFixtureKind.UniformAir : SectionFixtureKind.UniformStone,
            1 => SectionFixtureKind.Layered,
            2 => SectionFixtureKind.Mixed,
            3 => SectionFixtureKind.HighEntropy,
            _ => throw new InvalidOperationException("The E1 distribution cycle is incomplete."),
        };
    }

    private static E1MemoryDistribution MemoryDistributionForOrdinal(int ordinal)
    {
        return (ordinal % 4) switch
        {
            0 => E1MemoryDistribution.Homogeneous,
            1 => E1MemoryDistribution.Layered,
            2 => E1MemoryDistribution.Mixed,
            3 => E1MemoryDistribution.HighEntropy,
            _ => throw new InvalidOperationException("The E1 retained-memory distribution cycle is incomplete."),
        };
    }

    private static int CountMemoryDistribution(int cubeCount, E1MemoryDistribution distribution)
    {
        int count = 0;
        for (int ordinal = 0; ordinal < cubeCount; ordinal++)
        {
            if (MemoryDistributionForOrdinal(ordinal) == distribution)
            {
                count++;
            }
        }

        return count;
    }

    private static string SummaryDistributionName(SectionFixtureKind distribution)
    {
        return distribution switch
        {
            SectionFixtureKind.UniformAir or SectionFixtureKind.UniformStone => "homogeneous",
            SectionFixtureKind.Layered => "layered",
            SectionFixtureKind.Mixed => "mixed",
            SectionFixtureKind.HighEntropy => "highentropy",
            SectionFixtureKind.PaletteBoundary => "palette-boundary",
            _ => throw new InvalidOperationException("The E1 summary distribution is incomplete."),
        };
    }

    private static double Median(long[] sorted)
    {
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] / 2.0) + (sorted[middle] / 2.0);
    }

    private static void WriteReport(E1ReportDocument document, RawObservationSink raw, string? outputDirectory)
    {
        if (outputDirectory is null)
        {
            using Utf8JsonWriter writer = new(Console.OpenStandardOutput(), new JsonWriterOptions { Indented = true });
            WriteJson(writer, document, raw);
            writer.Flush();
            Console.WriteLine();
            Console.WriteLine(ToMarkdown(document));
            return;
        }

        _ = Directory.CreateDirectory(outputDirectory);
        string jsonPath = Path.Combine(outputDirectory, "e1-core-data-observation.json");
        using (FileStream stream = File.Create(jsonPath))
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteJson(writer, document, raw);
        }

        string markdownPath = Path.Combine(outputDirectory, "e1-core-data-observation.md");
        File.WriteAllText(markdownPath, ToMarkdown(document) + Environment.NewLine, Encoding.UTF8);
        Console.WriteLine($"Wrote observational E1 evidence to {Path.GetFullPath(outputDirectory)}");
    }

    private static void WriteJson(Utf8JsonWriter writer, E1ReportDocument document, RawObservationSink raw)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", "vibecraft.g1.e1.observation.v1");
        writer.WriteString("evidenceClassification", "observational");
        writer.WriteString("disposition", "defer");
        writer.WriteString("dispositionRationale", document.Decision.OverallRationale);
        writer.WriteString("fixtureId", SectionFixtureId);
        writer.WriteString("g0FixtureId", G0FixtureId);
        writer.WriteString("projectionFixtureId", ProjectionFixtureId);
        writer.WriteString("semanticFingerprintDomain", SemanticFingerprintDomain);
        writer.WriteString("seed", $"0x{Seed:X16}");
        writer.WritePropertyName("run");
        JsonSerializer.Serialize(writer, document.Run, JsonOptions);
        writer.WritePropertyName("manifest");
        JsonSerializer.Serialize(writer, document.Manifest, JsonOptions);
        writer.WritePropertyName("indexingValidation");
        JsonSerializer.Serialize(writer, document.IndexingValidation, JsonOptions);
        writer.WritePropertyName("paletteBoundaryDiagnostics");
        JsonSerializer.Serialize(writer, document.PaletteBoundaryDiagnostics, JsonOptions);
        writer.WritePropertyName("corpusFingerprints");
        JsonSerializer.Serialize(writer, document.CorpusFingerprints, JsonOptions);
        writer.WritePropertyName("metricDefinitions");
        JsonSerializer.Serialize(writer, document.MetricDefinitions, JsonOptions);
        writer.WritePropertyName("metricSummaries");
        JsonSerializer.Serialize(writer, document.MetricSummaries, JsonOptions);
        writer.WritePropertyName("amplificationSummaries");
        JsonSerializer.Serialize(writer, document.AmplificationSummaries, JsonOptions);
        writer.WritePropertyName("retainedMemory");
        JsonSerializer.Serialize(writer, document.Memory, JsonOptions);
        writer.WritePropertyName("provisionalAssessment");
        JsonSerializer.Serialize(writer, document.Decision, JsonOptions);
        writer.WritePropertyName("rejectedAlternatives");
        writer.WriteStartArray();
        writer.WriteStringValue("Freeze side16 from the architectural prior: rejected for this gate because the required G0 owner acceptance and real save/network measurements are absent.");
        writer.WriteStringValue("Freeze side32 from lower section-object count: rejected because object count alone does not satisfy the paired memory, timing, and amplification rules.");
        writer.WriteStringValue("Invent placeholder save or network encodings solely to complete this report: rejected because it would conflate the representation-neutral logical fixture with formats owned by later gates.");
        writer.WriteEndArray();
        writer.WritePropertyName("limitations");
        writer.WriteStartArray();
        writer.WriteStringValue("No section side, LocalIndex width, persistence layout, save encoding, network encoding, or user-world compatibility promise is selected.");
        writer.WriteStringValue("Timing observations are comparable only within the captured host, power mode, runtime, and GC configuration.");
        writer.WriteStringValue("Canonical logical-projection bytes are not storage, save, network, or wire bytes.");
        writer.WriteEndArray();
        raw.WriteJsonEvidence(writer);
        writer.WriteEndObject();
    }

    private static string ToMarkdown(E1ReportDocument document)
    {
        StringBuilder text = new();
        _ = text.Append("# G1/E1 core-data observations").AppendLine().AppendLine();
        _ = text.AppendLine("**Disposition: defer.** The predeclared G0 owner acceptance for host, runtime, GC, power, and product budgets is absent; this report cannot freeze compatibility constants.").AppendLine();
        _ = text.Append("Fixture set: `").Append(G0FixtureId).Append("`, `").Append(SectionFixtureId).Append("`, `").Append(ProjectionFixtureId).Append("`; seed `0x").Append(Seed.ToString("X16", CultureInfo.InvariantCulture)).AppendLine("`.").AppendLine();
        _ = text.AppendLine("## Protocol").AppendLine();
        _ = text.AppendLine("The corpus streams canonical 32-cubed semantic cubes. Ordinal modulo four selects homogeneous (alternating air/stone), layered, mixed, and high-entropy distributions. Every semantic fingerprint hashes the domain, fixture, seed, ordinal, distribution byte, and exactly 32,768 WorldStateId values in X-to-Z-to-Y order. One 32-cubed section and eight 16-cubed sections must match that semantic fingerprint; their logical-projection byte hashes are intentionally not required to match.").AppendLine();
        _ = text.Append("This profile fingerprints all ").Append(document.Run.CubeCount.ToString(CultureInfo.InvariantCulture))
            .Append(" corpus cubes and measures timing/amplification on ").Append(document.Run.CompletedPerformanceCubeCount.ToString(CultureInfo.InvariantCulture))
            .Append(" deterministic round-robin cubes. It applies ").Append(document.Run.EditClustersPerMeasuredCube.ToString(CultureInfo.InvariantCulture))
            .Append(" 4x4x4 cluster per measured cube and trace (").Append(document.Run.EditClustersPerTrace.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" clusters per trace in total), avoiding an unintended corpus-times-cluster cross-product.").AppendLine();
        _ = text.AppendLine("Measurements are same-machine paired Stopwatch observations. Orders alternate; raw samples include order, duration, checksum, allocations, and operation counts. One same-cube, same-round candidate/baseline pair is the raw unit. Decision summaries first take one median per cube across that cube's rounds (and traces for a grouped category), then bootstrap those cube-level units so repeated measurements are not treated as independent. No raw outlier is removed. Retained memory, when available, uses fresh child processes per distribution and is explicitly distinct from known logical payload bytes.").AppendLine();
        _ = text.AppendLine("## Provisional assessment").AppendLine();
        _ = text.Append(document.Decision.ProvisionalAssessment).AppendLine().AppendLine();
        _ = text.Append("Reason for overall defer: ").Append(document.Decision.OverallRationale).AppendLine().AppendLine();
        _ = text.AppendLine("| Criterion | Status | Evidence |")
            .AppendLine("| --- | --- | --- |");
        foreach (E1CriterionAssessment criterion in document.Decision.Criteria)
        {
            _ = text.Append("| ").Append(EscapeMarkdown(criterion.Name)).Append(" | ")
                .Append(criterion.Status.ToString().ToLowerInvariant()).Append(" | ")
                .Append(EscapeMarkdown(criterion.Evidence)).AppendLine(" |");
        }

        _ = text.AppendLine().AppendLine("## Metric summaries").AppendLine();
        _ = text.AppendLine("| Metric | Raw pairs | Independent units | Median ratio | MAD | 95% bootstrap interval | Definition |").AppendLine();
        _ = text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |").AppendLine();
        foreach (E1MetricSummary summary in document.MetricSummaries)
        {
            _ = text.Append("| ").Append(EscapeMarkdown(summary.Name)).Append(" | ")
                .Append(summary.SampleCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(summary.IndependentUnitCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(FormatDouble(summary.MedianRatio)).Append(" | ")
                .Append(FormatDouble(summary.Mad)).Append(" | ")
                .Append(FormatDouble(summary.BootstrapLower95)).Append('–').Append(FormatDouble(summary.BootstrapUpper95)).Append(" | ")
                .Append(EscapeMarkdown(summary.Definition)).AppendLine(" |");
        }

        _ = text.AppendLine().AppendLine("## Amplification").AppendLine();
        _ = text.AppendLine("Each natural window is one deterministic 4x4x4 (64-operation) cluster. Logical-projection bytes are bytes of the #8 canonical logical fixture for dirty semantic records; they are neither save nor wire bytes. Gross halo samples sum (side+2)^3 for dirty sections; unique halo samples deduplicate world-space sample coordinates inside a window.").AppendLine();
        _ = text.AppendLine("| Distribution | Layout / trace | Windows | Logical bytes p95 | Unique halo p95 | Gross halo p95 |").AppendLine();
        _ = text.AppendLine("| --- | --- | ---: | ---: | ---: | ---: |").AppendLine();
        foreach (E1AmplificationSummary summary in document.AmplificationSummaries)
        {
            _ = text.Append("| ").Append(summary.Distribution ?? "all (equal weight)").Append(" | ")
                .Append(summary.Layout).Append(" / ").Append(summary.Trace).Append(" | ")
                .Append(summary.WindowCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(summary.LogicalProjectionBytesP95.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(summary.UniqueHaloSamplesP95.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(summary.GrossHaloSamplesP95.ToString(CultureInfo.InvariantCulture)).AppendLine(" |");
        }

        _ = text.AppendLine().AppendLine("## Retained memory").AppendLine();
        _ = text.Append(document.Memory.Description).AppendLine();
        if (!document.Memory.IsAvailable)
        {
            _ = text.Append("Inconclusive: ").Append(document.Memory.InconclusiveReason).AppendLine();
        }
        else
        {
            _ = text.AppendLine("| Mode | Distribution | Cubes/trial | Valid / invalid trials | Median retained bytes | Median known payload bytes |")
                .AppendLine("| --- | --- | ---: | ---: | ---: | ---: |");
            foreach (E1MemoryModeSummary summary in document.Memory.ModeSummaries)
            {
                E1MemoryTrial? representativeTrial = document.Memory.Trials.FirstOrDefault(
                    trial => trial.Mode == summary.Mode && trial.Distribution == summary.Distribution && trial.IsValid);
                _ = text.Append("| ").Append(summary.Mode).Append(" | ")
                    .Append(summary.Distribution).Append(" | ")
                    .Append(representativeTrial?.SelectedCubeCount.ToString(CultureInfo.InvariantCulture) ?? "inconclusive").Append(" | ")
                    .Append(summary.ValidTrialCount.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(summary.InvalidTrialCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(summary.MedianRetainedBytes?.ToString("F0", CultureInfo.InvariantCulture) ?? "inconclusive").Append(" | ")
                    .Append(representativeTrial?.KnownPayloadBytes.ToString(CultureInfo.InvariantCulture) ?? "inconclusive").AppendLine(" |");
            }
        }

        _ = text.AppendLine().AppendLine("## Manifest").AppendLine();
        _ = text.Append("Observed host/runtime: ").Append(document.Manifest.OperatingSystem).Append(" / ")
            .Append(document.Manifest.Runtime).Append(" / SDK ").Append(document.Manifest.Sdk).AppendLine(".");
        _ = text.Append("CPU/power/GC: ").Append(document.Manifest.Cpu).Append(" / ")
            .Append(document.Manifest.PowerMode).Append(" / server GC=").Append(document.Manifest.ServerGc)
            .Append(", latency=").Append(document.Manifest.GcLatencyMode).AppendLine(".");
        _ = text.Append("Source: ").Append(document.Manifest.Commit).Append("; dirty=")
            .Append(document.Manifest.WorkingTreeDirty).Append("; source hash=")
            .Append(document.Manifest.SourceTreeSha256).AppendLine(".");
        _ = text.AppendLine().AppendLine("The companion JSON contains summary metadata and a SHA-256 reference to the bounded raw NDJSON observation artifact.");
        return text.ToString().TrimEnd();
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    private static string FormatDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString("F6", CultureInfo.InvariantCulture) : "n/a";
    }

    private static bool CrossesPredeclaredThreshold(E1MetricSummary summary)
    {
        if (!summary.BootstrapLower95.HasValue || !summary.BootstrapUpper95.HasValue)
        {
            return false;
        }

        double[] thresholds = summary.Name.StartsWith("adaptive-vs-dense/", StringComparison.Ordinal)
            ? [1.15]
            : summary.Name.StartsWith("section-side-reciprocal/", StringComparison.Ordinal)
                ? [1.15, 1.25]
                : summary.Name.StartsWith("section-side/", StringComparison.Ordinal)
                    ? [0.80, 1.15]
                    : [];
        foreach (double threshold in thresholds)
        {
            if (summary.BootstrapLower95 <= threshold && summary.BootstrapUpper95 >= threshold)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class E1RunAccumulator
    {
        private readonly E1Options options;
        private readonly RawObservationSink raw;
        private readonly Dictionary<string, List<E1RatioObservation>> ratios = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<long>> amplification = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<long>> distributionAmplification = new(StringComparer.Ordinal);
        private readonly List<E1PaletteBoundaryDiagnostic> paletteDiagnostics = [];
        private readonly IncrementalHash one32Corpus = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private readonly IncrementalHash eight16Corpus = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private int recordedCubes;
        private int recordedPerformanceCubes;
        private bool usedAdditionalRounds;

        internal E1RunAccumulator(E1Options options, RawObservationSink raw)
        {
            this.options = options;
            this.raw = raw;
            AppendUtf8(one32Corpus, "VC-G1-E1-CORPUS-FP-0.1.0");
            AppendUtf8(eight16Corpus, "VC-G1-E1-CORPUS-FP-0.1.0");
        }

        internal void RecordPaletteBoundaries()
        {
            foreach (int boundary in PaletteBoundaries)
            {
                WorldStateId[] canonical = SectionCandidateFixture.CreateStates(
                    SectionGeometry.Side32,
                    SectionFixtureKind.PaletteBoundary,
                    Seed,
                    boundary);
                WorldStateMap map = CreateWorldStateMap(canonical);
                MutableSectionBlockStates[] one32 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.OneSide32, canonical);
                MutableSectionBlockStates[] eight16 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, canonical);
                string semantic = ComputeSemanticFingerprint(canonical, -boundary, SectionFixtureKind.PaletteBoundary);
                ProjectionMeasurement side32 = EncodeProjection(one32, SectionEqualVolumeLayout.OneSide32, map, -boundary);
                ProjectionMeasurement side16 = EncodeProjection(eight16, SectionEqualVolumeLayout.EightSide16, map, -boundary);
                VerifySemanticCopies(canonical, one32, eight16, -boundary, SectionFixtureKind.PaletteBoundary, semantic);
                paletteDiagnostics.Add(new E1PaletteBoundaryDiagnostic(
                    boundary,
                    semantic,
                    side32.ByteLength,
                    side32.Digest,
                    side16.ByteLength,
                    side16.Digest));
            }
        }

        internal void RunCorpus()
        {
            RunCorpus(roundStart: 0, roundCount: options.PairedRounds, appendFingerprint: true);
            if (options.Profile == E1Profile.Full && NeedsAdditionalPairedRounds())
            {
                usedAdditionalRounds = true;
                RunCorpus(roundStart: options.PairedRounds, roundCount: 4, appendFingerprint: false);
            }
        }

        private void RunCorpus(int roundStart, int roundCount, bool appendFingerprint)
        {
            for (int ordinal = 0; ordinal < options.CubeCount; ordinal++)
            {
                if (!appendFingerprint && ordinal >= options.PerformanceCubeCount)
                {
                    continue;
                }

                SectionFixtureKind distribution = DistributionForOrdinal(ordinal);
                WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(distribution, CubeSeedFor(ordinal));
                WorldStateMap map = CreateWorldStateMap(canonical);
                MutableSectionBlockStates[] one32 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.OneSide32, canonical);
                MutableSectionBlockStates[] eight16 = SectionEqualVolumeFixture.CreateSections(SectionEqualVolumeLayout.EightSide16, canonical);
                string semantic = ComputeSemanticFingerprint(canonical, ordinal, distribution);
                VerifySemanticCopies(canonical, one32, eight16, ordinal, distribution, semantic);

                ProjectionMeasurement one32Projection = EncodeProjection(one32, SectionEqualVolumeLayout.OneSide32, map, ordinal);
                ProjectionMeasurement eight16Projection = EncodeProjection(eight16, SectionEqualVolumeLayout.EightSide16, map, ordinal);
                if (appendFingerprint)
                {
                    AppendCorpus(one32Corpus, ordinal, semantic, one32Projection);
                    AppendCorpus(eight16Corpus, ordinal, semantic, eight16Projection);
                }

                if (ordinal >= options.PerformanceCubeCount)
                {
                    if (appendFingerprint)
                    {
                        recordedCubes++;
                    }

                    continue;
                }

                int[] randomTrace = SectionBenchmarkSupport.CreateRandomTrace(
                    SectionBenchmarkSupport.RandomTraceLength,
                    CubeVolume,
                    CubeSeedFor(ordinal) ^ 0xE7037ED1A0B428DBUL);
                RunReadPairs(ordinal, distribution, canonical, one32, eight16, map, randomTrace, roundStart, roundCount);
                RunSnapshotAndProjectionPairs(ordinal, distribution, one32, eight16, map, roundStart, roundCount);
                foreach (SectionEditTraceKind traceKind in Enum.GetValues<SectionEditTraceKind>())
                {
                    SectionEdit[] trace = SectionEqualVolumeFixture.CreateEditTrace(
                        canonical,
                        traceKind,
                        CubeSeedFor(ordinal),
                        options.ClustersPerMeasuredCube);
                    RunEditPairs(ordinal, distribution, canonical, trace, traceKind, roundStart, roundCount);
                    if (appendFingerprint)
                    {
                        RecordAmplification(ordinal, distribution, canonical, trace, traceKind, SectionEqualVolumeLayout.OneSide32);
                        RecordAmplification(ordinal, distribution, canonical, trace, traceKind, SectionEqualVolumeLayout.EightSide16);
                    }
                }

                if (appendFingerprint)
                {
                    recordedCubes++;
                    recordedPerformanceCubes++;
                }
            }
        }

        internal E1ReportDocument CreateDocument(SectionObservationManifest manifest, E1MemoryReport memory)
        {
            E1MetricSummary[] timingSummaries = CreateTimingSummaries();
            E1MetricSummary[] metricSummaries =
            [
                .. timingSummaries,
                .. CreateMemoryRatioSummaries(memory),
            ];
            E1AmplificationSummary[] amplificationSummaries =
            [
                .. amplification.Keys
                    .Select(key => key[..key.LastIndexOf('/')])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .Select(key => E1AmplificationSummary.Create(
                        key,
                        amplification[$"{key}/logical-projection-bytes"],
                        amplification[$"{key}/unique-halo-samples"],
                        amplification[$"{key}/gross-halo-samples"])),
                .. distributionAmplification.Keys
                    .Select(key => key[..key.LastIndexOf('/')])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .Select(key => E1AmplificationSummary.CreateDistribution(
                        key,
                        distributionAmplification[$"{key}/logical-projection-bytes"],
                        distributionAmplification[$"{key}/unique-halo-samples"],
                        distributionAmplification[$"{key}/gross-halo-samples"])),
            ];
            E1DecisionAssessment decision = E1DecisionAssessment.Create(options.Profile, metricSummaries, amplificationSummaries, memory);
            return new E1ReportDocument(
                new E1RunConfiguration(
                    options.Profile,
                    options.CubeCount,
                    options.PairedRounds,
                    options.MemoryTrials,
                    options.TotalClustersPerTrace,
                    options.BootstrapResamples,
                    options.MaxBytes,
                    recordedCubes,
                    recordedPerformanceCubes,
                    options.ClustersPerMeasuredCube,
                    Stopwatch.Frequency,
                    4,
                    "DOTNET_TieredCompilation=0; DOTNET_TieredPGO=0; enforced by a fresh report child process",
                    "timing uses Stopwatch.GetTimestamp after four read warmups; allocation uses a checksum-matched independent read probe after four additional warmups",
                    "raw unit: one same-cube, same-round ordered pair; decision unit: one cube median across all its rounds/traces in the grouped category; bootstrap resamples cube-level units",
                    true,
                    DateTimeOffset.UtcNow,
                    usedAdditionalRounds
                        ? "six alternating paired rounds plus four additional rounds because at least one initial bootstrap interval crossed a predeclared threshold"
                        : "six alternating paired rounds; CI uses one round and never makes a performance decision"),
                manifest,
                new E1IndexingValidation(true, "Exhaustive collision-free X-to-Z-to-Y bijection verified for side 16 and side 32."),
                [.. paletteDiagnostics],
                new E1CorpusFingerprints(
                    Convert.ToHexStringLower(one32Corpus.GetHashAndReset()),
                    Convert.ToHexStringLower(eight16Corpus.GetHashAndReset()),
                    "Each corpus fingerprint hashes ordered (ordinal, semantic hash, logical-projection byte length, logical-projection hash) tuples for its own layout. Cross-layout logical bytes are not asserted equal."),
                MetricDefinitions(),
                metricSummaries,
                amplificationSummaries,
                memory,
                decision);
        }

        private void RunReadPairs(
            int ordinal,
            SectionFixtureKind distribution,
            WorldStateId[] canonical,
            MutableSectionBlockStates[] one32,
            MutableSectionBlockStates[] eight16,
            WorldStateMap map,
            int[] randomTrace,
            int roundStart,
            int roundCount)
        {
            _ = map;
            for (int round = roundStart; round < roundStart + roundCount; round++)
            {
                Pair(
                    ordinal, distribution, round, "adaptive-vs-dense/random-read/one-side32", "one-side32", "dense", randomTrace.Length,
                    () => MeasureRandomRead(one32, SectionEqualVolumeLayout.OneSide32, randomTrace),
                    () => MeasureDenseRandomRead(canonical, randomTrace));
                Pair(
                    ordinal, distribution, round, "adaptive-vs-dense/random-read/eight-side16", "eight-side16", "dense", randomTrace.Length,
                    () => MeasureRandomRead(eight16, SectionEqualVolumeLayout.EightSide16, randomTrace),
                    () => MeasureDenseRandomRead(canonical, randomTrace));
                Pair(
                    ordinal, distribution, round, "adaptive-vs-dense/linear-read/one-side32", "one-side32", "dense", SectionBenchmarkSupport.RandomTraceLength,
                    () => MeasureLinearRead(one32, SectionEqualVolumeLayout.OneSide32),
                    () => MeasureDenseLinearRead(canonical));
                Pair(
                    ordinal, distribution, round, "adaptive-vs-dense/linear-read/eight-side16", "eight-side16", "dense", SectionBenchmarkSupport.RandomTraceLength,
                    () => MeasureLinearRead(eight16, SectionEqualVolumeLayout.EightSide16),
                    () => MeasureDenseLinearRead(canonical));
                Pair(
                    ordinal, distribution, round, "section-side/random-read", "one-side32", "eight-side16", randomTrace.Length,
                    () => MeasureRandomRead(one32, SectionEqualVolumeLayout.OneSide32, randomTrace),
                    () => MeasureRandomRead(eight16, SectionEqualVolumeLayout.EightSide16, randomTrace));
                Pair(
                    ordinal, distribution, round, "section-side/linear-read", "one-side32", "eight-side16", SectionBenchmarkSupport.RandomTraceLength,
                    () => MeasureLinearRead(one32, SectionEqualVolumeLayout.OneSide32),
                    () => MeasureLinearRead(eight16, SectionEqualVolumeLayout.EightSide16));
            }
        }

        private void RunSnapshotAndProjectionPairs(
            int ordinal,
            SectionFixtureKind distribution,
            MutableSectionBlockStates[] one32,
            MutableSectionBlockStates[] eight16,
            WorldStateMap map,
            int roundStart,
            int roundCount)
        {
            for (int round = roundStart; round < roundStart + roundCount; round++)
            {
                Pair(
                    ordinal, distribution, round, "section-side/snapshot", "one-side32", "eight-side16", CubeVolume,
                    () => MeasureSnapshots(one32),
                    () => MeasureSnapshots(eight16),
                    requireSameChecksum: false);
                Pair(
                    ordinal, distribution, round, "section-side/logical-projection", "one-side32", "eight-side16", CubeVolume,
                    () => MeasureProjection(one32, SectionEqualVolumeLayout.OneSide32, map, ordinal),
                    () => MeasureProjection(eight16, SectionEqualVolumeLayout.EightSide16, map, ordinal),
                    requireSameChecksum: false);
            }
        }

        private void RunEditPairs(
            int ordinal,
            SectionFixtureKind distribution,
            WorldStateId[] canonical,
            SectionEdit[] trace,
            SectionEditTraceKind traceKind,
            int roundStart,
            int roundCount)
        {
            string traceName = traceKind == SectionEditTraceKind.InteriorClusters ? "interior" : "boundary";
            for (int round = roundStart; round < roundStart + roundCount; round++)
            {
                Pair(
                    ordinal, distribution, round, $"adaptive-vs-dense/clustered-edit/{traceName}/one-side32", "one-side32", "dense", trace.Length,
                    () => MeasureEdits(canonical, SectionEqualVolumeLayout.OneSide32, trace),
                    () => MeasureDenseEdits(canonical, trace));
                Pair(
                    ordinal, distribution, round, $"adaptive-vs-dense/clustered-edit/{traceName}/eight-side16", "eight-side16", "dense", trace.Length,
                    () => MeasureEdits(canonical, SectionEqualVolumeLayout.EightSide16, trace),
                    () => MeasureDenseEdits(canonical, trace));
                Pair(
                    ordinal, distribution, round, $"section-side/clustered-edit/{traceName}", "one-side32", "eight-side16", trace.Length,
                    () => MeasureEdits(canonical, SectionEqualVolumeLayout.OneSide32, trace),
                    () => MeasureEdits(canonical, SectionEqualVolumeLayout.EightSide16, trace));
            }
        }

        private void Pair(
            int ordinal,
            SectionFixtureKind distribution,
            int round,
            string metric,
            string numeratorName,
            string denominatorName,
            int operationCount,
            Func<E1MeasuredOperation> numerator,
            Func<E1MeasuredOperation> denominator,
            bool requireSameChecksum = true)
        {
            bool numeratorFirst = round % 2 == 0;
            E1MeasuredOperation numeratorMeasurement;
            E1MeasuredOperation denominatorMeasurement;
            if (numeratorFirst)
            {
                numeratorMeasurement = numerator();
                denominatorMeasurement = denominator();
            }
            else
            {
                denominatorMeasurement = denominator();
                numeratorMeasurement = numerator();
            }

            if (requireSameChecksum && numeratorMeasurement.Checksum != denominatorMeasurement.Checksum)
            {
                throw new InvalidOperationException($"Semantic checksum mismatch for {metric}, cube {ordinal}, round {round}.");
            }

            raw.Record(new E1RawObservation(
                "paired-measurement",
                ordinal,
                distribution.ToString(),
                metric,
                round,
                numeratorFirst ? 0 : 1,
                numeratorName,
                numeratorMeasurement.DurationTicks,
                numeratorMeasurement.AllocatedBytes,
                numeratorMeasurement.Checksum.ToString("x16", CultureInfo.InvariantCulture),
                operationCount,
                numeratorMeasurement.LogicalByteLength,
                numeratorMeasurement.LogicalDigest,
                null,
                null,
                null,
                null,
                null));
            raw.Record(new E1RawObservation(
                "paired-measurement",
                ordinal,
                distribution.ToString(),
                metric,
                round,
                numeratorFirst ? 1 : 0,
                denominatorName,
                denominatorMeasurement.DurationTicks,
                denominatorMeasurement.AllocatedBytes,
                denominatorMeasurement.Checksum.ToString("x16", CultureInfo.InvariantCulture),
                operationCount,
                denominatorMeasurement.LogicalByteLength,
                denominatorMeasurement.LogicalDigest,
                null,
                null,
                null,
                null,
                null));
            if (metric.Contains("read", StringComparison.Ordinal) && numeratorMeasurement.AllocatedBytes != 0)
            {
                AddRatio("validation/nonzero-warmed-read-allocation", ordinal, round, 1);
            }

            if (metric.Contains("read", StringComparison.Ordinal) && denominatorMeasurement.AllocatedBytes != 0)
            {
                AddRatio("validation/nonzero-warmed-read-allocation", ordinal, round, 1);
            }

            if (numeratorMeasurement.DurationTicks <= 0 || denominatorMeasurement.DurationTicks <= 0)
            {
                AddRatio("validation/nonpositive-duration", ordinal, round, 1);
            }
            else
            {
                double ratio = numeratorMeasurement.DurationTicks / (double)denominatorMeasurement.DurationTicks;
                AddRatio(metric, ordinal, round, ratio);
                string distributionName = SummaryDistributionName(distribution);
                AddRatio($"distribution/{distributionName}/{metric}", ordinal, round, ratio);
                AddReciprocalSectionSideRatio(metric, ordinal, round, ratio);
                if (metric.StartsWith("section-side/", StringComparison.Ordinal))
                {
                    AddRatio(
                        $"distribution/{distributionName}/section-side-reciprocal/{metric["section-side/".Length..]}",
                        ordinal,
                        round,
                        ratio == 0 ? double.PositiveInfinity : 1 / ratio);
                }
            }
        }

        private void RecordAmplification(
            int ordinal,
            SectionFixtureKind distribution,
            WorldStateId[] canonical,
            SectionEdit[] trace,
            SectionEditTraceKind traceKind,
            SectionEqualVolumeLayout layout)
        {
            MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
            for (int windowStart = 0; windowStart < trace.Length; windowStart += SectionEqualVolumeFixture.EditsPerCluster)
            {
                int windowLength = Math.Min(SectionEqualVolumeFixture.EditsPerCluster, trace.Length - windowStart);
                HashSet<int> dirtySections = [];
                for (int offset = 0; offset < windowLength; offset++)
                {
                    SectionEdit edit = trace[windowStart + offset];
                    SectionWriteResult result = SectionEqualVolumeFixture.SetGlobalUnchecked(sections, layout, edit);
                    if (result == SectionWriteResult.Changed)
                    {
                        _ = dirtySections.Add(SectionEqualVolumeFixture.GetSectionIndexForGlobal(layout, edit.GlobalIndex));
                    }
                }

                WorldStateId[] semantic = CopyCanonicalFromSnapshots(sections, layout);
                WorldStateMap map = CreateWorldStateMap(semantic);
                ProjectionMeasurement projection = EncodeProjection(sections, layout, map, ordinal, dirtySections);
                long grossHalo = 0;
                long logicalValues = 0;
                long knownPayload = 0;
                foreach (int sectionIndex in dirtySections)
                {
                    SectionBlockStateSnapshot snapshot = sections[sectionIndex].CaptureSnapshot();
                    int side = snapshot.Geometry.Side.Value;
                    grossHalo = checked(grossHalo + ((long)(side + 2) * (side + 2) * (side + 2)));
                    logicalValues = checked(logicalValues + snapshot.Count);
                    knownPayload = checked(knownPayload + snapshot.GetStorageMetrics().KnownPayloadBytes);
                }

                long uniqueHalo = CountUniqueHaloSamples(layout, dirtySections);
                string key = $"{LayoutName(layout)}/{traceKind}";
                AddAmplification($"{key}/logical-projection-bytes", projection.ByteLength);
                AddAmplification($"{key}/unique-halo-samples", uniqueHalo);
                AddAmplification($"{key}/gross-halo-samples", grossHalo);
                string distributionKey = $"{SummaryDistributionName(distribution)}/{key}";
                AddDistributionAmplification($"{distributionKey}/logical-projection-bytes", projection.ByteLength);
                AddDistributionAmplification($"{distributionKey}/unique-halo-samples", uniqueHalo);
                AddDistributionAmplification($"{distributionKey}/gross-halo-samples", grossHalo);
                raw.Record(new E1RawObservation(
                    "amplification-window",
                    ordinal,
                    distribution.ToString(),
                    key,
                    windowStart / SectionEqualVolumeFixture.EditsPerCluster,
                    null,
                    LayoutName(layout),
                    null,
                    null,
                    null,
                    windowLength,
                    projection.ByteLength,
                    projection.Digest,
                    dirtySections.Count,
                    logicalValues,
                    knownPayload,
                    grossHalo,
                    uniqueHalo));
            }
        }

        private void AddRatio(string metric, int cubeOrdinal, int round, double ratio)
        {
            if (!ratios.TryGetValue(metric, out List<E1RatioObservation>? values))
            {
                values = [];
                ratios.Add(metric, values);
            }

            values.Add(new E1RatioObservation(cubeOrdinal, round, ratio));
        }

        private void AddReciprocalSectionSideRatio(string metric, int cubeOrdinal, int round, double ratio)
        {
            if (!metric.StartsWith("section-side/", StringComparison.Ordinal))
            {
                return;
            }

            AddRatio(
                $"section-side-reciprocal/{metric["section-side/".Length..]}",
                cubeOrdinal,
                round,
                ratio == 0 ? double.PositiveInfinity : 1 / ratio);
        }

        private bool NeedsAdditionalPairedRounds()
        {
            foreach (E1MetricSummary summary in CreateTimingSummaries())
            {
                if (CrossesPredeclaredThreshold(summary))
                {
                    return true;
                }
            }

            return false;
        }

        private E1MetricSummary[] CreateTimingSummaries()
        {
            List<E1MetricSummary> summaries =
            [
                .. ratios.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => E1MetricSummary.Create(pair.Key, pair.Value, options.BootstrapResamples, Seed)),
            ];
            AddGroupedReadSummary("section-side", summaries);
            AddGroupedReadSummary("section-side-reciprocal", summaries);
            AddGroupedEditSummary("section-side", summaries);
            AddGroupedEditSummary("section-side-reciprocal", summaries);
            return [.. summaries.OrderBy(summary => summary.Name, StringComparer.Ordinal)];
        }

        private void AddGroupedReadSummary(string prefix, List<E1MetricSummary> summaries)
        {
            string[] names = [$"{prefix}/random-read", $"{prefix}/linear-read"];
            List<E1RatioObservation> values =
            [
                .. names.Where(ratios.ContainsKey)
                    .SelectMany(name => ratios[name]),
            ];
            if (values.Count > 0)
            {
                summaries.Add(E1MetricSummary.Create($"{prefix}/read", values, options.BootstrapResamples, Seed));
            }
        }

        private void AddGroupedEditSummary(string prefix, List<E1MetricSummary> summaries)
        {
            List<E1RatioObservation> values =
            [
                .. ratios.Where(pair => pair.Key.StartsWith($"{prefix}/clustered-edit/", StringComparison.Ordinal))
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .SelectMany(pair => pair.Value),
            ];
            if (values.Count > 0)
            {
                summaries.Add(E1MetricSummary.Create($"{prefix}/clustered-edit", values, options.BootstrapResamples, Seed));
            }
        }

        private E1MetricSummary[] CreateMemoryRatioSummaries(E1MemoryReport memory)
        {
            if (memory.Trials.Length == 0)
            {
                return [];
            }

            List<E1MetricSummary> summaries = [];
            foreach (E1MemoryDistribution distribution in Enum.GetValues<E1MemoryDistribution>())
            {
                string distributionName = distribution.ToString().ToLowerInvariant();
                foreach (E1MetricSummary? summary in new E1MetricSummary?[]
                {
                    CreateMemoryRatioSummary(memory, E1MemoryMode.OneSide32Adaptive, E1MemoryMode.DenseCanonical, distribution, $"fresh-process-memory/{distributionName}/one-side32-vs-dense"),
                    CreateMemoryRatioSummary(memory, E1MemoryMode.EightSide16Adaptive, E1MemoryMode.DenseCanonical, distribution, $"fresh-process-memory/{distributionName}/eight-side16-vs-dense"),
                    CreateMemoryRatioSummary(memory, E1MemoryMode.OneSide32Adaptive, E1MemoryMode.EightSide16Adaptive, distribution, $"fresh-process-memory/{distributionName}/section-side"),
                    CreateMemoryRatioSummary(memory, E1MemoryMode.EightSide16Adaptive, E1MemoryMode.OneSide32Adaptive, distribution, $"fresh-process-memory/{distributionName}/section-side-reciprocal"),
                })
                {
                    if (summary is not null)
                    {
                        summaries.Add(summary);
                    }
                }
            }

            foreach ((E1MemoryMode mode, string candidate) in new[]
            {
                (E1MemoryMode.OneSide32Adaptive, "one-side32"),
                (E1MemoryMode.EightSide16Adaptive, "eight-side16"),
            })
            {
                E1MetricSummary? equalWeight = CreateEqualWeightMemoryRatioSummary(
                    memory,
                    mode,
                    $"fresh-process-memory/homogeneous-layered-mixed/{candidate}-vs-dense");
                if (equalWeight is not null)
                {
                    summaries.Add(equalWeight);
                }
            }

            E1MetricSummary? balancedSectionSide = CreateMemoryRatioSummary(
                memory,
                E1MemoryMode.OneSide32Adaptive,
                E1MemoryMode.EightSide16Adaptive,
                null,
                "fresh-process-memory/balanced/section-side");
            E1MetricSummary? balancedSectionSideReciprocal = CreateMemoryRatioSummary(
                memory,
                E1MemoryMode.EightSide16Adaptive,
                E1MemoryMode.OneSide32Adaptive,
                null,
                "fresh-process-memory/balanced/section-side-reciprocal");
            if (balancedSectionSide is not null)
            {
                summaries.Add(balancedSectionSide);
            }

            if (balancedSectionSideReciprocal is not null)
            {
                summaries.Add(balancedSectionSideReciprocal);
            }

            return [.. summaries];
        }

        private E1MetricSummary? CreateEqualWeightMemoryRatioSummary(
            E1MemoryReport memory,
            E1MemoryMode numeratorMode,
            string name)
        {
            E1MemoryDistribution[] distributions =
            [
                E1MemoryDistribution.Homogeneous,
                E1MemoryDistribution.Layered,
                E1MemoryDistribution.Mixed,
            ];
            List<double> trialRatios = [];
            foreach (int trialIndex in memory.Trials.Select(trial => trial.TrialIndex).Distinct().Order())
            {
                List<double> distributionRatios = [];
                foreach (E1MemoryDistribution distribution in distributions)
                {
                    E1MemoryTrial? numerator = memory.Trials.SingleOrDefault(trial =>
                        trial.Mode == numeratorMode && trial.Distribution == distribution &&
                        trial.TrialIndex == trialIndex && trial.IsValid && trial.RetainedBytes > 0);
                    E1MemoryTrial? denominator = memory.Trials.SingleOrDefault(trial =>
                        trial.Mode == E1MemoryMode.DenseCanonical && trial.Distribution == distribution &&
                        trial.TrialIndex == trialIndex && trial.IsValid && trial.RetainedBytes > 0);
                    if (numerator is null || denominator is null || !AreComparableMemoryGroups([numerator], [denominator], 1))
                    {
                        distributionRatios.Clear();
                        break;
                    }

                    distributionRatios.Add(numerator.RetainedBytes / (double)denominator.RetainedBytes);
                }

                if (distributionRatios.Count == distributions.Length)
                {
                    trialRatios.Add(distributionRatios.Average());
                }
            }

            return trialRatios.Count == 0
                ? null
                : E1MetricSummary.Create(name, trialRatios, options.BootstrapResamples, Seed);
        }

        private E1MetricSummary? CreateMemoryRatioSummary(
            E1MemoryReport memory,
            E1MemoryMode numerator,
            E1MemoryMode denominator,
            E1MemoryDistribution? distribution,
            string name)
        {
            Dictionary<int, E1MemoryTrial[]> numeratorTrials = memory.Trials
                .Where(trial => trial.Mode == numerator && trial.IsValid && trial.RetainedBytes > 0 &&
                    (!distribution.HasValue || trial.Distribution == distribution.Value))
                .GroupBy(trial => trial.TrialIndex)
                .ToDictionary(group => group.Key, group => group.OrderBy(trial => trial.Distribution).ToArray());
            Dictionary<int, E1MemoryTrial[]> denominatorTrials = memory.Trials
                .Where(trial => trial.Mode == denominator && trial.IsValid && trial.RetainedBytes > 0 &&
                    (!distribution.HasValue || trial.Distribution == distribution.Value))
                .GroupBy(trial => trial.TrialIndex)
                .ToDictionary(group => group.Key, group => group.OrderBy(trial => trial.Distribution).ToArray());
            List<double> values = [];
            foreach ((int trialIndex, E1MemoryTrial[] denominatorGroup) in denominatorTrials)
            {
                if (numeratorTrials.TryGetValue(trialIndex, out E1MemoryTrial[]? numeratorGroup) &&
                    AreComparableMemoryGroups(numeratorGroup, denominatorGroup, distribution.HasValue ? 1 : 4))
                {
                    long numeratorBytes = numeratorGroup.Sum(trial => trial.RetainedBytes);
                    long denominatorBytes = denominatorGroup.Sum(trial => trial.RetainedBytes);
                    values.Add(numeratorBytes / (double)denominatorBytes);
                }
            }

            return values.Count == 0 ? null : E1MetricSummary.Create(name, values, options.BootstrapResamples, Seed);
        }

        private static bool AreComparableMemoryGroups(
            E1MemoryTrial[] numerator,
            E1MemoryTrial[] denominator,
            int expectedCount)
        {
            if (numerator.Length != expectedCount || denominator.Length != expectedCount)
            {
                return false;
            }

            for (int index = 0; index < expectedCount; index++)
            {
                E1MemoryTrial left = numerator[index];
                E1MemoryTrial right = denominator[index];
                if (left.Distribution != right.Distribution ||
                    left.CubeCount != right.CubeCount ||
                    left.SelectedCubeCount != right.SelectedCubeCount ||
                    left.MaxBytes != right.MaxBytes ||
                    !string.Equals(left.SemanticChecksum, right.SemanticChecksum, StringComparison.Ordinal) ||
                    !string.Equals(left.Runtime, right.Runtime, StringComparison.Ordinal) ||
                    !string.Equals(left.ProcessArchitecture, right.ProcessArchitecture, StringComparison.Ordinal) ||
                    left.ServerGc != right.ServerGc ||
                    !string.Equals(left.GcLatencyMode, right.GcLatencyMode, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddAmplification(string metric, long value)
        {
            if (!amplification.TryGetValue(metric, out List<long>? values))
            {
                values = [];
                amplification.Add(metric, values);
            }

            values.Add(value);
        }

        private void AddDistributionAmplification(string metric, long value)
        {
            if (!distributionAmplification.TryGetValue(metric, out List<long>? values))
            {
                values = [];
                distributionAmplification.Add(metric, values);
            }

            values.Add(value);
        }
    }

    private static E1MeasuredOperation MeasureRandomRead(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        int[] trace)
    {
        WarmRead(() => ReadRandom(sections, layout, trace));
        long beforeTicks = Stopwatch.GetTimestamp();
        ulong timingChecksum = ReadRandom(sections, layout, trace);
        long duration = Stopwatch.GetTimestamp() - beforeTicks;
        WarmRead(() => ReadRandom(sections, layout, trace));
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        ulong allocationChecksum = ReadRandom(sections, layout, trace);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        return CreateReadMeasurement(duration, allocated, timingChecksum, allocationChecksum);
    }

    private static E1MeasuredOperation MeasureDenseRandomRead(WorldStateId[] states, int[] trace)
    {
        WarmRead(() => ReadDenseRandom(states, trace));
        long beforeTicks = Stopwatch.GetTimestamp();
        ulong timingChecksum = ReadDenseRandom(states, trace);
        long duration = Stopwatch.GetTimestamp() - beforeTicks;
        WarmRead(() => ReadDenseRandom(states, trace));
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        ulong allocationChecksum = ReadDenseRandom(states, trace);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        return CreateReadMeasurement(duration, allocated, timingChecksum, allocationChecksum);
    }

    private static E1MeasuredOperation MeasureLinearRead(IReadOnlySectionBlockStates[] sections, SectionEqualVolumeLayout layout)
    {
        WarmRead(() => ReadLinear(sections, layout));
        long beforeTicks = Stopwatch.GetTimestamp();
        ulong timingChecksum = ReadLinear(sections, layout);
        long duration = Stopwatch.GetTimestamp() - beforeTicks;
        WarmRead(() => ReadLinear(sections, layout));
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        ulong allocationChecksum = ReadLinear(sections, layout);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        return CreateReadMeasurement(duration, allocated, timingChecksum, allocationChecksum);
    }

    private static E1MeasuredOperation MeasureDenseLinearRead(WorldStateId[] states)
    {
        WarmRead(() => ReadDenseLinear(states));
        long beforeTicks = Stopwatch.GetTimestamp();
        ulong timingChecksum = ReadDenseLinear(states);
        long duration = Stopwatch.GetTimestamp() - beforeTicks;
        WarmRead(() => ReadDenseLinear(states));
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        ulong allocationChecksum = ReadDenseLinear(states);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        return CreateReadMeasurement(duration, allocated, timingChecksum, allocationChecksum);
    }

    private static void WarmRead(Func<ulong> operation)
    {
        for (int iteration = 0; iteration < 4; iteration++)
        {
            _ = operation();
        }
    }

    private static E1MeasuredOperation CreateReadMeasurement(
        long duration,
        long allocated,
        ulong timingChecksum,
        ulong allocationChecksum)
    {
        return allocationChecksum != timingChecksum
            ? throw new InvalidOperationException("A warmed read allocation probe produced a different semantic checksum than its timing probe.")
            : new E1MeasuredOperation(duration, allocated, timingChecksum, null, null);
    }

    private static E1MeasuredOperation MeasureSnapshots(MutableSectionBlockStates[] sections)
    {
        _ = SnapshotCreationChecksum(sections);
        return Measure(() => SnapshotCreationChecksum(sections));
    }

    private static E1MeasuredOperation MeasureProjection(
        MutableSectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        WorldStateMap map,
        int ordinal)
    {
        _ = EncodeProjection(sections, layout, map, ordinal);
        return MeasureProjectionCore(() => EncodeProjection(sections, layout, map, ordinal));
    }

    private static E1MeasuredOperation MeasureEdits(
        WorldStateId[] canonical,
        SectionEqualVolumeLayout layout,
        SectionEdit[] trace)
    {
        MutableSectionBlockStates[] warm = SectionEqualVolumeFixture.CreateSections(layout, canonical);
        _ = ApplyEdits(warm, layout, trace);
        MutableSectionBlockStates[] candidate = SectionEqualVolumeFixture.CreateSections(layout, canonical);
        E1MeasuredOperation measurement = Measure(() => ApplyEdits(candidate, layout, trace));
        WorldStateId[] actual = CopyCanonicalFromSnapshots(candidate, layout);
        return VerifyAndIncludeEditedSemantics(measurement, canonical, trace, actual);
    }

    private static E1MeasuredOperation MeasureDenseEdits(WorldStateId[] canonical, SectionEdit[] trace)
    {
        WorldStateId[] warm = (WorldStateId[])canonical.Clone();
        _ = ApplyDenseEdits(warm, trace);
        WorldStateId[] candidate = (WorldStateId[])canonical.Clone();
        E1MeasuredOperation measurement = Measure(() => ApplyDenseEdits(candidate, trace));
        return VerifyAndIncludeEditedSemantics(measurement, canonical, trace, candidate);
    }

    private static E1MeasuredOperation VerifyAndIncludeEditedSemantics(
        E1MeasuredOperation measurement,
        WorldStateId[] canonical,
        SectionEdit[] trace,
        WorldStateId[] actual)
    {
        WorldStateId[] expected = (WorldStateId[])canonical.Clone();
        _ = ApplyDenseEdits(expected, trace);
        if (!actual.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidOperationException("A measured edit candidate produced incorrect final semantic block states.");
        }

        ulong semanticChecksum = SectionBenchmarkSupport.Checksum(actual);
        return measurement with { Checksum = unchecked((measurement.Checksum * 31UL) ^ semanticChecksum) };
    }

    private static E1MeasuredOperation Measure(Func<ulong> operation)
    {
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        long beforeTicks = Stopwatch.GetTimestamp();
        ulong checksum = operation();
        long duration = Stopwatch.GetTimestamp() - beforeTicks;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        return new E1MeasuredOperation(duration, allocated, checksum, null, null);
    }

    private static E1MeasuredOperation MeasureProjectionCore(Func<ProjectionMeasurement> operation)
    {
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        long beforeTicks = Stopwatch.GetTimestamp();
        ProjectionMeasurement result = operation();
        long duration = Stopwatch.GetTimestamp() - beforeTicks;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        return new E1MeasuredOperation(duration, allocated, result.Checksum, result.ByteLength, result.Digest);
    }

    private static ulong ReadRandom(IReadOnlySectionBlockStates[] sections, SectionEqualVolumeLayout layout, int[] trace)
    {
        ulong checksum = 0;
        foreach (int index in trace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetGlobalUnchecked(sections, layout, index).Value);
        }

        return checksum;
    }

    private static ulong ReadDenseRandom(WorldStateId[] states, int[] trace)
    {
        ulong checksum = 0;
        foreach (int index in trace)
        {
            checksum = unchecked((checksum * 0x100000001B3UL) ^ states[index].Value);
        }

        return checksum;
    }

    private static ulong ReadLinear(IReadOnlySectionBlockStates[] sections, SectionEqualVolumeLayout layout)
    {
        ulong checksum = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 0; index < CubeVolume; index++)
            {
                checksum = unchecked((checksum * 0x100000001B3UL) ^ SectionEqualVolumeFixture.GetGlobalUnchecked(sections, layout, index).Value);
            }
        }

        return checksum;
    }

    private static ulong ReadDenseLinear(ReadOnlySpan<WorldStateId> states)
    {
        ulong checksum = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (WorldStateId state in states)
            {
                checksum = unchecked((checksum * 0x100000001B3UL) ^ state.Value);
            }
        }

        return checksum;
    }

    private static ulong SnapshotCreationChecksum(MutableSectionBlockStates[] sections)
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (MutableSectionBlockStates section in sections)
        {
            SectionBlockStateSnapshot snapshot = section.CaptureSnapshot();
            SectionStorageMetrics metrics = snapshot.GetStorageMetrics();
            checksum = unchecked((checksum ^ checked((ulong)snapshot.Geometry.Side.Value)) * 0x100000001B3UL);
            checksum = unchecked((checksum ^ checked((ulong)snapshot.Revision.Value)) * 0x100000001B3UL);
            checksum = unchecked((checksum ^ checked((ulong)snapshot.Count)) * 0x100000001B3UL);
            checksum = unchecked((checksum ^ (ulong)snapshot.StorageKind) * 0x100000001B3UL);
            checksum = unchecked((checksum ^ checked((ulong)metrics.KnownPayloadBytes)) * 0x100000001B3UL);
        }

        return checksum;
    }

    private static ulong ApplyEdits(MutableSectionBlockStates[] sections, SectionEqualVolumeLayout layout, SectionEdit[] trace)
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (SectionEdit edit in trace)
        {
            checksum = SectionBenchmarkSupport.AddEditChecksum(
                checksum,
                edit,
                SectionEqualVolumeFixture.SetGlobalUnchecked(sections, layout, edit));
        }

        return checksum;
    }

    private static ulong ApplyDenseEdits(WorldStateId[] states, SectionEdit[] trace)
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (SectionEdit edit in trace)
        {
            SectionWriteResult result = states[edit.GlobalIndex].Equals(edit.State)
                ? SectionWriteResult.Unchanged
                : SetDenseUnchecked(states, edit);
            checksum = SectionBenchmarkSupport.AddEditChecksum(checksum, edit, result);
        }

        return checksum;
    }

    private static SectionWriteResult SetDenseUnchecked(Span<WorldStateId> states, SectionEdit edit)
    {
        states[edit.GlobalIndex] = edit.State;
        return SectionWriteResult.Changed;
    }

    private static void VerifySemanticCopies(
        WorldStateId[] canonical,
        MutableSectionBlockStates[] one32,
        MutableSectionBlockStates[] eight16,
        int ordinal,
        SectionFixtureKind distribution,
        string expectedFingerprint)
    {
        WorldStateId[] one32Copy = CopyCanonicalFromSnapshots(one32, SectionEqualVolumeLayout.OneSide32);
        WorldStateId[] eight16Copy = CopyCanonicalFromSnapshots(eight16, SectionEqualVolumeLayout.EightSide16);
        if (!one32Copy.AsSpan().SequenceEqual(canonical) || !eight16Copy.AsSpan().SequenceEqual(canonical))
        {
            throw new InvalidOperationException($"Snapshot semantic copy differs from the dense cube for ordinal {ordinal}.");
        }

        string one32Hash = ComputeSemanticFingerprint(one32Copy, ordinal, distribution);
        string eight16Hash = ComputeSemanticFingerprint(eight16Copy, ordinal, distribution);
        if (!string.Equals(expectedFingerprint, one32Hash, StringComparison.Ordinal) ||
            !string.Equals(expectedFingerprint, eight16Hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Semantic fingerprint differs across dense/one-32/eight-16 layouts for ordinal {ordinal}.");
        }
    }

    private static WorldStateId[] CopyCanonicalFromSnapshots(MutableSectionBlockStates[] sections, SectionEqualVolumeLayout layout)
    {
        IReadOnlySectionBlockStates[] snapshots = new IReadOnlySectionBlockStates[sections.Length];
        for (int index = 0; index < sections.Length; index++)
        {
            snapshots[index] = sections[index].CaptureSnapshot();
        }

        WorldStateId[] result = new WorldStateId[CubeVolume];
        SectionEqualVolumeFixture.CopyToCanonicalUnchecked(
            snapshots,
            layout,
            result,
            SectionBenchmarkSupport.CreateSide16Scratch());
        return result;
    }

    private static ProjectionMeasurement EncodeProjection(
        MutableSectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        WorldStateMap map,
        int ordinal,
        IEnumerable<int>? selectedSectionIndices = null)
    {
        HashSet<int>? selected = selectedSectionIndices is null ? null : [.. selectedSectionIndices];
        HashSet<WorldStateId>? selectedStateIds = selected is null ? null : [new WorldStateId(0)];
        List<LogicalSectionInput> inputs = [];
        for (int index = 0; index < sections.Length; index++)
        {
            if (selected is not null && !selected.Contains(index))
            {
                continue;
            }

            SectionBlockStateSnapshot snapshot = sections[index].CaptureSnapshot();
            WorldStateId[] semantic = new WorldStateId[snapshot.Count];
            snapshot.CopyTo(semantic);
            if (selectedStateIds is not null)
            {
                foreach (WorldStateId state in semantic)
                {
                    _ = selectedStateIds.Add(state);
                }
            }

            inputs.Add(new LogicalSectionInput(CreateLogicalRecordKey(layout, ordinal, index), snapshot.Geometry, semantic));
        }

        if (inputs.Count == 0)
        {
            return new ProjectionMeasurement(0, "none", 0);
        }

        WorldStateMap projectionMap = selectedStateIds is null
            ? map
            : WorldStateMap.Restore(map.Bindings.Where(binding => selectedStateIds.Contains(binding.Id)));
        LogicalProjectionEncoding encoding = CanonicalLogicalProjectionCodecV1.Encode(CanonicalLogicalProjection.Create(projectionMap, inputs));
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (byte value in encoding.Bytes)
        {
            checksum = unchecked((checksum ^ value) * 0x100000001B3UL);
        }

        return new ProjectionMeasurement(encoding.Bytes.Length, encoding.Digest.ToString(), checksum);
    }

    private static LogicalRecordKey CreateLogicalRecordKey(SectionEqualVolumeLayout layout, int ordinal, int sectionIndex)
    {
        const uint dimension = 1;
        if (layout == SectionEqualVolumeLayout.OneSide32)
        {
            return new LogicalRecordKey(
                LogicalRecordKind.SectionState,
                new DimensionId(dimension),
                new SectionCoord(ordinal, 0, 0));
        }

        SectionEqualVolumeFixture.GetSectionCoordinates(layout, sectionIndex, out int x, out int y, out int z);
        return new LogicalRecordKey(
            LogicalRecordKind.SectionState,
            new DimensionId(dimension),
            new SectionCoord(checked((ordinal * 2L) + x), y, z));
    }

    private static WorldStateMap CreateWorldStateMap(ReadOnlySpan<WorldStateId> states)
    {
        HashSet<uint> ids = [];
        foreach (WorldStateId state in states)
        {
            _ = ids.Add(state.Value);
        }

        List<WorldStateBinding> bindings = [new WorldStateBinding(new WorldStateId(0), CanonicalBlockState.Air)];
        foreach (uint value in ids.Order())
        {
            if (value == 0)
            {
                continue;
            }

            bindings.Add(new WorldStateBinding(new WorldStateId(value), CreateCanonicalState(value)));
        }

        return WorldStateMap.Restore(bindings);
    }

    private static CanonicalBlockState CreateCanonicalState(uint id)
    {
        ContentKey block = ContentKey.Create("fixture", $"state-{id.ToString(CultureInfo.InvariantCulture)}");
        return id % 5 == 0
            ? new CanonicalBlockState(block, [BlockStateProperty.Create(ContentKey.Create("fixture", "variant"), (id % 17).ToString(CultureInfo.InvariantCulture))])
            : new CanonicalBlockState(block, []);
    }

    private static string ComputeSemanticFingerprint(
        ReadOnlySpan<WorldStateId> canonical,
        int ordinal,
        SectionFixtureKind distribution)
    {
        if (canonical.Length != CubeVolume)
        {
            throw new ArgumentException($"E1 semantic fingerprint requires exactly {CubeVolume} states.", nameof(canonical));
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hash, SemanticFingerprintDomain);
        hash.AppendData([0]);
        AppendUtf8(hash, SectionFixtureId);
        hash.AppendData([0]);
        Span<byte> fixedFields = stackalloc byte[sizeof(ulong) + sizeof(int) + sizeof(byte)];
        BinaryPrimitives.WriteUInt64LittleEndian(fixedFields, Seed);
        BinaryPrimitives.WriteInt32LittleEndian(fixedFields[sizeof(ulong)..], ordinal);
        fixedFields[^1] = (byte)distribution;
        hash.AppendData(fixedFields);
        Span<byte> stateBytes = stackalloc byte[sizeof(uint)];
        for (int y = 0; y < CubeSide; y++)
        {
            for (int z = 0; z < CubeSide; z++)
            {
                for (int x = 0; x < CubeSide; x++)
                {
                    int index = x + (CubeSide * (z + (CubeSide * y)));
                    BinaryPrimitives.WriteUInt32LittleEndian(stateBytes, canonical[index].Value);
                    hash.AppendData(stateBytes);
                }
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendCorpus(IncrementalHash hash, int ordinal, string semanticHash, ProjectionMeasurement projection)
    {
        Span<byte> ordinalBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(ordinalBytes, ordinal);
        hash.AppendData(ordinalBytes);
        hash.AppendData(Convert.FromHexString(semanticHash));
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, projection.ByteLength);
        hash.AppendData(lengthBytes);
        hash.AppendData(Convert.FromHexString(projection.Digest));
    }

    private static void AppendUtf8(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
    }

    private static long CountUniqueHaloSamples(SectionEqualVolumeLayout layout, HashSet<int> dirtySections)
    {
        HashSet<(int X, int Y, int Z)> unique = [];
        foreach (int sectionIndex in dirtySections)
        {
            SectionEqualVolumeFixture.GetSectionCoordinates(layout, sectionIndex, out int sectionX, out int sectionY, out int sectionZ);
            int side = layout == SectionEqualVolumeLayout.OneSide32 ? 32 : 16;
            int originX = sectionX * side;
            int originY = sectionY * side;
            int originZ = sectionZ * side;
            for (int y = -1; y <= side; y++)
            {
                for (int z = -1; z <= side; z++)
                {
                    for (int x = -1; x <= side; x++)
                    {
                        _ = unique.Add((originX + x, originY + y, originZ + z));
                    }
                }
            }
        }

        return unique.Count;
    }

    private static string LayoutName(SectionEqualVolumeLayout layout)
    {
        return layout == SectionEqualVolumeLayout.OneSide32 ? "one-side32" : "eight-side16";
    }

    private static E1MetricDefinition[] MetricDefinitions()
    {
        return
        [
            new E1MetricDefinition("adaptive-vs-dense/*", "lower is better: adaptive Stopwatch ticks divided by dense Stopwatch ticks, paired on the same cube and alternating order"),
            new E1MetricDefinition("section-side/*", "lower is better: one-side32 Stopwatch ticks divided by eight-side16 Stopwatch ticks, paired on the same cube and alternating order"),
            new E1MetricDefinition("section-side/snapshot", "snapshot creation only: capture one immutable snapshot per candidate section and consume O(1) snapshot metadata inside the timed interval; equal-volume semantic reconstruction is verified outside the timed interval"),
            new E1MetricDefinition("known-payload-bytes", "diagnostic owned adaptive scalar/array payload bytes; not retained process memory and not storage/save/network/wire bytes"),
            new E1MetricDefinition("logical-projection-bytes", "exact #8 canonical logical-projection fixture bytes; not save/network/wire bytes"),
            new E1MetricDefinition("unique/gross-halo-samples", "per 4x4x4 edit window; remesh input samples, not rendered mesh time"),
        ];
    }

    private sealed class RawObservationSink : IDisposable
    {
        private readonly List<E1RawObservation>? inMemory;
        private readonly string? rawPath;
        private StreamWriter? rawWriter;
        private long observationCount;
        private long rawByteCount;

        internal RawObservationSink(string? outputDirectory)
        {
            if (outputDirectory is null)
            {
                inMemory = [];
                return;
            }

            _ = Directory.CreateDirectory(outputDirectory);
            rawPath = Path.Combine(outputDirectory, "e1-core-data-raw.ndjson");
            rawWriter = new StreamWriter(File.Create(rawPath), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        internal void Record(E1RawObservation observation)
        {
            if (inMemory is not null)
            {
                inMemory.Add(observation);
                observationCount++;
                return;
            }

            string line = JsonSerializer.Serialize(observation, RawJsonOptions);
            long lineBytes = checked(Encoding.UTF8.GetByteCount(line) + Encoding.UTF8.GetByteCount(Environment.NewLine));
            if (checked(rawByteCount + lineBytes) > MaxRawObservationBytes)
            {
                throw new IOException($"The E1 raw observation artifact exceeded its fixed {MaxRawObservationBytes}-byte ceiling.");
            }

            rawWriter!.WriteLine(line);
            rawByteCount += lineBytes;
            observationCount++;
        }

        internal void WriteJsonEvidence(Utf8JsonWriter writer)
        {
            if (inMemory is not null)
            {
                writer.WritePropertyName("rawObservations");
                writer.WriteStartArray();
                foreach (E1RawObservation observation in inMemory)
                {
                    JsonSerializer.Serialize(writer, observation, JsonOptions);
                }

                writer.WriteEndArray();
                return;
            }

            rawWriter?.Dispose();
            rawWriter = null;
            if (rawPath is null)
            {
                throw new InvalidOperationException("The E1 raw observation artifact path is unavailable.");
            }

            using FileStream stream = File.OpenRead(rawPath);
            using SHA256 sha256 = SHA256.Create();
            string digest = Convert.ToHexStringLower(sha256.ComputeHash(stream));
            writer.WritePropertyName("rawObservationArtifact");
            writer.WriteStartObject();
            writer.WriteString("path", Path.GetFileName(rawPath));
            writer.WriteNumber("observationCount", observationCount);
            writer.WriteNumber("byteCount", new FileInfo(rawPath).Length);
            writer.WriteNumber("maximumByteCount", MaxRawObservationBytes);
            writer.WriteString("sha256", digest);
            writer.WriteString("format", "newline-delimited JSON; one complete raw observation object per UTF-8 line");
            writer.WriteEndObject();
        }

        public void Dispose()
        {
            rawWriter?.Dispose();
            rawWriter = null;
        }
    }

    private sealed record RetainedCorpus(object[] Roots, long KnownPayloadBytes, ulong SemanticChecksum);

    private readonly record struct ProjectionMeasurement(int ByteLength, string Digest, ulong Checksum);

    private readonly record struct E1MeasuredOperation(
        long DurationTicks,
        long AllocatedBytes,
        ulong Checksum,
        int? LogicalByteLength,
        string? LogicalDigest);

    private readonly record struct E1RatioObservation(int CubeOrdinal, int Round, double Ratio);

    private enum E1Profile : byte
    {
        Ci = 0,
        Full = 1,
    }

    private enum E1MemoryMode : byte
    {
        DenseCanonical = 0,
        OneSide32Adaptive = 1,
        EightSide16Adaptive = 2,
    }

    private enum E1MemoryDistribution : byte
    {
        Homogeneous = 0,
        Layered = 1,
        Mixed = 2,
        HighEntropy = 3,
    }

    private sealed record E1Options(
        E1Profile Profile,
        int CubeCount,
        int PairedRounds,
        int MemoryTrials,
        int ClusterCount,
        int BootstrapResamples,
        long MaxBytes,
        string? OutputDirectory)
    {
        internal int PerformanceCubeCount => Profile == E1Profile.Full ? ClusterCount : CubeCount;

        internal int ClustersPerMeasuredCube => Profile == E1Profile.Full ? 1 : ClusterCount;

        internal int TotalClustersPerTrace => checked(PerformanceCubeCount * ClustersPerMeasuredCube);

        internal static E1Options Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Dictionary<string, string> values = ParseArguments(args);
            E1Profile profile = values.TryGetValue("--profile", out string? profileText)
                ? ParseProfile(profileText)
                : E1Profile.Ci;
            int expectedCubes = profile == E1Profile.Ci ? 8 : 12_500;
            int expectedRounds = profile == E1Profile.Ci ? 1 : 6;
            int expectedTrials = profile == E1Profile.Ci ? 1 : 9;
            int expectedClusters = profile == E1Profile.Ci ? 2 : 256;
            int expectedResamples = profile == E1Profile.Ci ? 100 : 10_000;
            long defaultMaxBytes = profile == E1Profile.Ci ? 256L * 1024 * 1024 : 4L * 1024 * 1024 * 1024;
            int cubes = ParseExactInt(values, "--cubes", expectedCubes);
            int rounds = ParseExactInt(values, "--rounds", expectedRounds);
            int trials = ParseExactInt(values, "--memory-trials", expectedTrials);
            int clusters = ParseExactInt(values, "--clusters", expectedClusters);
            int resamples = ParseExactInt(values, "--bootstrap-resamples", expectedResamples);
            long maxBytes = ParseBoundedLong(values, "--max-bytes", defaultMaxBytes, 32L * 1024 * 1024, MaxRequestedBytes);
            if (values.TryGetValue("--seed", out string? seedText) && ParseUInt64(seedText, "--seed") != Seed)
            {
                throw new ArgumentException($"--seed must be the predeclared 0x{Seed:X16}.");
            }

            if (values.TryGetValue("--fixture", out string? fixture) && !string.Equals(fixture, G0FixtureId, StringComparison.Ordinal))
            {
                throw new ArgumentException($"--fixture must be {G0FixtureId}.");
            }

            string? output = values.TryGetValue("--output", out string? outputValue)
                ? Path.GetFullPath(outputValue)
                : null;
            if (profile == E1Profile.Full && output is null)
            {
                throw new ArgumentException("The full profile requires --output so its complete raw artifact is bounded on disk rather than buffered for stdout.");
            }

            long estimatedDenseBytes = checked((long)cubes * CubeVolume * DenseValueBytes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(estimatedDenseBytes, maxBytes, nameof(args));

            return new E1Options(profile, cubes, rounds, trials, clusters, resamples, maxBytes, output);
        }
    }

    private sealed record E1MemoryChildOptions(
        E1Profile Profile,
        E1MemoryMode Mode,
        E1MemoryDistribution Distribution,
        int TrialIndex,
        int LaunchOrder,
        int CubeCount,
        long MaxBytes)
    {
        internal static E1MemoryChildOptions Parse(string[] args)
        {
            Dictionary<string, string> values = ParseArguments(args);
            if (!values.TryGetValue("--profile", out string? profileText) ||
                !values.TryGetValue("--mode", out string? modeText) ||
                !values.TryGetValue("--distribution", out string? distributionText) ||
                !values.TryGetValue("--trial-index", out string? trialText) ||
                !values.TryGetValue("--launch-order", out string? launchOrderText) ||
                !values.TryGetValue("--cubes", out string? cubeText) ||
                !values.TryGetValue("--max-bytes", out string? bytesText))
            {
                throw new ArgumentException("The E1 retained-memory child requires --profile, --mode, --distribution, --trial-index, --launch-order, --cubes, and --max-bytes.");
            }

            E1Profile profile = ParseProfile(profileText);
            if (!Enum.TryParse(modeText, ignoreCase: false, out E1MemoryMode mode) || !Enum.IsDefined(mode))
            {
                throw new ArgumentException("--mode is not an E1 retained-memory mode.", nameof(args));
            }

            if (!Enum.TryParse(distributionText, ignoreCase: false, out E1MemoryDistribution distribution) || !Enum.IsDefined(distribution))
            {
                throw new ArgumentException("--distribution is not an E1 retained-memory distribution.", nameof(args));
            }

            int expectedCubes = profile == E1Profile.Ci ? 8 : 12_500;
            int cubeCount = ParseInt(cubeText, "--cubes");
            if (cubeCount != expectedCubes)
            {
                throw new ArgumentException($"--cubes must be {expectedCubes} for profile {profile}.");
            }

            int trialIndex = ParseInt(trialText, "--trial-index");
            int maximumTrialIndex = profile == E1Profile.Ci ? 0 : 8;
            if (trialIndex < 0 || trialIndex > maximumTrialIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "The trial index is outside the fixed profile bounds.");
            }

            int launchOrder = ParseInt(launchOrderText, "--launch-order");
            int maximumLaunchOrder = profile == E1Profile.Ci ? 11 : 107;
            if (launchOrder < 0 || launchOrder > maximumLaunchOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "The memory-child launch order is outside the fixed profile bounds.");
            }

            long maxBytes = ParseLong(bytesText, "--max-bytes");
            return maxBytes is < (32L * 1024 * 1024) or > MaxRequestedBytes
                ? throw new ArgumentOutOfRangeException(nameof(args), "The retained-memory child max-byte bound is outside the permitted range.")
                : new E1MemoryChildOptions(profile, mode, distribution, trialIndex, launchOrder, cubeCount, maxBytes);
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        HashSet<string> allowed =
        [
            "--profile", "--fixture", "--seed", "--cubes", "--rounds", "--memory-trials", "--clusters", "--bootstrap-resamples", "--max-bytes", "--output",
            "--mode", "--distribution", "--trial-index", "--launch-order",
        ];
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string name = args[index];
            if (!allowed.Contains(name) || index == args.Length - 1 || values.ContainsKey(name))
            {
                throw new ArgumentException($"Unknown, incomplete, or duplicate E1 argument: {name}");
            }

            values.Add(name, args[++index]);
        }

        return values;
    }

    private static E1Profile ParseProfile(string value)
    {
        return value switch
        {
            "ci" => E1Profile.Ci,
            "full" => E1Profile.Full,
            _ => throw new ArgumentException("--profile must be ci or full."),
        };
    }

    private static int ParseExactInt(Dictionary<string, string> values, string name, int expected)
    {
        if (!values.TryGetValue(name, out string? value))
        {
            return expected;
        }

        int parsed = ParseInt(value, name);
        return parsed == expected
            ? parsed
            : throw new ArgumentException($"{name} must be {expected} for the selected fixed profile.");
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw new ArgumentException($"{name} must be a decimal integer.");
    }

    private static long ParseBoundedLong(Dictionary<string, string> values, string name, long defaultValue, long minimum, long maximum)
    {
        if (!values.TryGetValue(name, out string? value))
        {
            return defaultValue;
        }

        long parsed = ParseLong(value, name);
        return parsed >= minimum && parsed <= maximum
            ? parsed
            : throw new ArgumentOutOfRangeException(name, $"{name} must be in the range {minimum} through {maximum}.");
    }

    private static long ParseLong(string value, string name)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : throw new ArgumentException($"{name} must be a decimal integer.");
    }

    private static ulong ParseUInt64(string value, string name)
    {
        NumberStyles style = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? NumberStyles.AllowHexSpecifier
            : NumberStyles.None;
        string digits = style == NumberStyles.AllowHexSpecifier ? value[2..] : value;
        return ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out ulong parsed)
            ? parsed
            : throw new ArgumentException($"{name} must be an unsigned decimal or 0x-prefixed hexadecimal integer.");
    }

    private sealed record E1RunConfiguration(
        E1Profile Profile,
        int CubeCount,
        int PairedRounds,
        int FreshProcessMemoryTrialsPerMode,
        int EditClustersPerTrace,
        int BootstrapResamples,
        long MaxBytes,
        int CompletedCubeCount,
        int CompletedPerformanceCubeCount,
        int EditClustersPerMeasuredCube,
        long StopwatchFrequency,
        int ReadWarmupRepetitions,
        string RuntimeJitSettings,
        string TimerAndAllocationMethod,
        string StatisticalUnit,
        bool Completed,
        DateTimeOffset CompletedAtUtc,
        string AdaptiveRoundRule);

    private sealed record E1IndexingValidation(bool Succeeded, string Description);

    private sealed record E1PaletteBoundaryDiagnostic(
        int PaletteSize,
        string SemanticFingerprint,
        int OneSide32LogicalBytes,
        string OneSide32LogicalHash,
        int EightSide16LogicalBytes,
        string EightSide16LogicalHash);

    private sealed record E1CorpusFingerprints(string OneSide32, string EightSide16, string Definition);

    private sealed record E1MetricDefinition(string Name, string Definition);

    private sealed record E1MetricSummary(
        string Name,
        string Definition,
        int SampleCount,
        int IndependentUnitCount,
        double? MedianRatio,
        double? Mad,
        double? BootstrapLower95,
        double? BootstrapUpper95)
    {
        internal static E1MetricSummary Create(string name, List<double> values, int resamples, ulong seed)
        {
            return CreateCore(name, values.Count, [.. values.Order()], resamples, seed);
        }

        internal static E1MetricSummary Create(string name, List<E1RatioObservation> observations, int resamples, ulong seed)
        {
            double[] cubeMedians =
            [
                .. observations.GroupBy(observation => observation.CubeOrdinal)
                    .OrderBy(group => group.Key)
                    .Select(group => Median([.. group.Select(observation => observation.Ratio).Order()])),
            ];
            return CreateCore(name, observations.Count, [.. cubeMedians.Order()], resamples, seed);
        }

        private static E1MetricSummary CreateCore(
            string name,
            int sampleCount,
            double[] orderedIndependentUnits,
            int resamples,
            ulong seed)
        {
            double[] ordered = orderedIndependentUnits;
            if (ordered.Length == 0)
            {
                return new E1MetricSummary(name, DefinitionFor(name), sampleCount, 0, null, null, null, null);
            }

            double median = Median(ordered);
            double mad = Median([.. ordered.Select(value => Math.Abs(value - median)).Order()]);
            (double lower, double upper) = BootstrapMedianInterval(ordered, resamples, seed ^ StableMetricSeed(name));
            return new E1MetricSummary(name, DefinitionFor(name), sampleCount, ordered.Length, median, mad, lower, upper);
        }

        private static string DefinitionFor(string name)
        {
            return name.StartsWith("distribution/", StringComparison.Ordinal)
                ? "distribution-scoped observation; remaining path components use the corresponding aggregate definition"
                : name.StartsWith("adaptive-vs-dense", StringComparison.Ordinal)
                ? "paired lower-is-better adaptive/dense duration ratio"
                : name.StartsWith("section-side-reciprocal", StringComparison.Ordinal)
                ? "paired lower-is-better side16/side32 duration ratio"
                : name.StartsWith("section-side", StringComparison.Ordinal)
                ? "paired lower-is-better one-side32/eight-side16 duration ratio"
                : name.StartsWith("fresh-process-memory", StringComparison.Ordinal)
                ? "paired fresh-process retained-memory ratio; lower is better"
                : name.StartsWith("validation", StringComparison.Ordinal)
                ? "count marker for nonzero warmed read allocation observations"
                : "raw observation ratio";
        }
    }

    private static ulong StableMetricSeed(string name)
    {
        ulong hash = 0xCBF29CE484222325UL;
        foreach (byte value in Encoding.UTF8.GetBytes(name))
        {
            hash ^= value;
            hash = unchecked(hash * 0x100000001B3UL);
        }

        return hash;
    }

    private sealed record E1AmplificationSummary(
        string? Distribution,
        string Layout,
        string Trace,
        int WindowCount,
        long LogicalProjectionBytesP95,
        long UniqueHaloSamplesP95,
        long GrossHaloSamplesP95)
    {
        internal static E1AmplificationSummary Create(
            string key,
            List<long> logicalProjectionBytes,
            List<long> uniqueHaloSamples,
            List<long> grossHaloSamples)
        {
            string[] parts = key.Split('/', StringSplitOptions.None);
            return new E1AmplificationSummary(
                null,
                parts[0],
                parts[1],
                logicalProjectionBytes.Count,
                P95(logicalProjectionBytes),
                P95(uniqueHaloSamples),
                P95(grossHaloSamples));
        }

        internal static E1AmplificationSummary CreateDistribution(
            string key,
            List<long> logicalProjectionBytes,
            List<long> uniqueHaloSamples,
            List<long> grossHaloSamples)
        {
            string[] parts = key.Split('/', StringSplitOptions.None);
            return new E1AmplificationSummary(
                parts[0],
                parts[1],
                parts[2],
                logicalProjectionBytes.Count,
                P95(logicalProjectionBytes),
                P95(uniqueHaloSamples),
                P95(grossHaloSamples));
        }

        private static long P95(List<long> values)
        {
            long[] ordered = [.. values.Order()];
            return ordered[checked((int)Math.Ceiling(ordered.Length * 0.95) - 1)];
        }
    }

    private sealed record E1MemoryTrial(
        E1MemoryMode Mode,
        E1MemoryDistribution Distribution,
        int TrialIndex,
        int LaunchOrder,
        int ProcessId,
        int CubeCount,
        int SelectedCubeCount,
        long MaxBytes,
        long KnownPayloadBytes,
        string SemanticChecksum,
        string Runtime,
        string ProcessArchitecture,
        bool ServerGc,
        string GcLatencyMode,
        long BeforeBytes,
        long AfterBytes,
        long RetainedBytes,
        bool IsValid,
        string? InconclusiveReason);

    private sealed record E1MemoryModeSummary(
        E1MemoryMode Mode,
        E1MemoryDistribution Distribution,
        int ValidTrialCount,
        int InvalidTrialCount,
        bool IsConclusive,
        long? MinimumRetainedBytes,
        double? MedianRetainedBytes,
        long? MaximumRetainedBytes,
        double? MadRetainedBytes);

    private sealed record E1MemoryReport(
        bool IsAvailable,
        string? InconclusiveReason,
        string Description,
        E1MemoryModeSummary[] ModeSummaries,
        E1MemoryTrial[] Trials);

    private sealed record E1RawObservation(
        string Kind,
        int CubeOrdinal,
        string Distribution,
        string Metric,
        int RoundOrWindow,
        int? ExecutionOrder,
        string Candidate,
        long? DurationTicks,
        long? AllocatedBytes,
        string? Checksum,
        int OperationCount,
        int? LogicalProjectionBytes,
        string? LogicalProjectionHash,
        int? DirtySectionCount,
        long? LogicalValuesRepublished,
        long? KnownPayloadBytes,
        long? GrossHaloSamples,
        long? UniqueHaloSamples);

    private enum E1CriterionStatus : byte
    {
        Pass = 0,
        Fail = 1,
        Inconclusive = 2,
        Blocked = 3,
    }

    private sealed record E1CriterionAssessment(string Name, E1CriterionStatus Status, string Evidence);

    private sealed record E1DecisionAssessment(
        bool PerformanceDecisionEligible,
        string ProvisionalAssessment,
        E1CriterionAssessment[] Criteria,
        string OverallRationale,
        string ThresholdProtocol)
    {
        internal static E1DecisionAssessment Create(
            E1Profile profile,
            E1MetricSummary[] metrics,
            E1AmplificationSummary[] amplification,
            E1MemoryReport memory)
        {
            bool isFull = profile == E1Profile.Full;
            List<E1CriterionAssessment> criteria =
            [
                new(
                    "fixed-full-profile",
                    isFull ? E1CriterionStatus.Pass : E1CriterionStatus.Inconclusive,
                    isFull
                        ? "The fixed 12,500-cube, six-round, nine-memory-trial, 10,000-resample profile completed."
                        : "The CI smoke profile validates plumbing and invariants only; it cannot support a performance decision."),
                new(
                    "semantic-and-projection-correctness",
                    E1CriterionStatus.Pass,
                    "The report completed exhaustive side16/side32 indexing, equal-volume semantic checks, palette-boundary diagnostics, snapshots, and deterministic canonical logical projections without an invariant failure."),
            ];
            bool zeroReadAllocation = !metrics.Any(metric => string.Equals(metric.Name, "validation/nonzero-warmed-read-allocation", StringComparison.Ordinal));
            criteria.Add(new E1CriterionAssessment(
                "zero-allocation-warmed-reads",
                zeroReadAllocation ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                zeroReadAllocation
                    ? "Every measured warmed random and linear read reported exactly zero thread allocations."
                    : "At least one warmed random or linear read reported a nonzero thread allocation; see the validation metric and raw observations."));
            bool positiveDurations = !metrics.Any(metric => string.Equals(metric.Name, "validation/nonpositive-duration", StringComparison.Ordinal));
            criteria.Add(new E1CriterionAssessment(
                "positive-measured-durations",
                positiveDurations ? E1CriterionStatus.Pass : E1CriterionStatus.Inconclusive,
                positiveDurations
                    ? "Every paired duration was positive; no equality ratio was synthesized."
                    : "At least one paired duration was nonpositive. Its ratio was omitted rather than synthesized."));

            AddAdaptiveMemoryCriterion(criteria, profile, metrics, memory, "one-side32");
            AddAdaptiveMemoryCriterion(criteria, profile, metrics, memory, "eight-side16");
            AddAdaptiveTimingCriterion(criteria, profile, metrics, "one-side32");
            AddAdaptiveTimingCriterion(criteria, profile, metrics, "eight-side16");
            AddSectionSideCriteria(criteria, profile, metrics, amplification, preferSide32: true);
            AddSectionSideCriteria(criteria, profile, metrics, amplification, preferSide32: false);
            criteria.Add(new E1CriterionAssessment(
                "save-and-network-amplification",
                E1CriterionStatus.Inconclusive,
                "No G1 save format or network encoding exists to measure. Canonical logical-projection bytes are retained as a representation-neutral republish proxy and are not relabeled as storage or wire evidence."));
            criteria.Add(new E1CriterionAssessment(
                "g0-owner-acceptance",
                E1CriterionStatus.Blocked,
                "No owner acceptance exists for this host, runtime, GC mode, power mode, or the applicable G0 product budgets."));

            bool provisionalSide32 = isFull && Passes(criteria, "save-and-network-amplification") && Passes(criteria, "positive-measured-durations") && Passes(criteria, "adaptive-memory-one-side32") &&
                Passes(criteria, "adaptive-timing-one-side32") && Passes(criteria, "side32-memory") &&
                Passes(criteria, "side32-primary") && Passes(criteria, "side32-amplification");
            bool provisionalSide16 = isFull && Passes(criteria, "save-and-network-amplification") && Passes(criteria, "positive-measured-durations") && Passes(criteria, "adaptive-memory-eight-side16") &&
                Passes(criteria, "adaptive-timing-eight-side16") && Passes(criteria, "side16-primary") &&
                Passes(criteria, "side16-amplification");
            string provisional = profile == E1Profile.Ci
                ? "CI smoke completed. Its timings and one-trial memory deltas are diagnostic only; no candidate is provisionally selected."
                : provisionalSide32
                    ? "The measured criteria provisionally favor one side32 adaptive section for the equal-volume fixture. This is observational and cannot freeze a constant."
                    : provisionalSide16
                        ? "The measured criteria provisionally retain eight side16 adaptive sections for the equal-volume fixture. This is observational and cannot freeze a constant."
                        : "Neither candidate satisfies every predeclared metric prerequisite in this observation. Thresholds remain unchanged and no candidate is selected.";
            return new E1DecisionAssessment(
                false,
                provisional,
                [.. criteria],
                "G0 owner acceptance of the benchmark host, runtime, GC, power conditions, and applicable product budgets is explicitly absent. The issue protocol therefore requires defer even if any provisional metric rule appears to pass.",
                "Adaptive rule: equal-weight homogeneous/layered/mixed retained block-state memory <= 50% of dense; HighEntropy <= 110%; upper 95% adaptive/dense time ratio <= 1.15 for random reads, linear reads, and clustered edits; warmed reads exactly 0 B/op; snapshot/projection semantics pass. Section-side rule: side32 overturns the side16 prior only with retained-memory upper ratio <= 0.80, at least three read/linear/edit/snapshot/projection upper ratios <= 0.80, no primary upper > 1.15, and p95 canonical-logical-byte/unique-halo amplification <= 2x side16. Otherwise side16 may be selected only with equal-weight geometric mean <= 1.15x side32, no primary > 1.25x, and p95 amplification <= 2x side32. Threshold-crossing intervals add four paired rounds; still ambiguous is defer. G0 absence always makes the overall result defer.");
        }

        private static void AddAdaptiveMemoryCriterion(
            List<E1CriterionAssessment> criteria,
            E1Profile profile,
            E1MetricSummary[] metrics,
            E1MemoryReport memory,
            string candidate)
        {
            string name = $"adaptive-memory-{candidate}";
            if (profile != E1Profile.Full)
            {
                criteria.Add(new E1CriterionAssessment(name, E1CriterionStatus.Inconclusive, "The CI profile has one retained-memory trial per mode/distribution and is not decision-eligible."));
                return;
            }

            List<string> observations = [];
            bool available = memory.IsAvailable;
            bool passes = true;
            foreach ((string distribution, double threshold) in new[]
            {
                ("homogeneous-layered-mixed", 0.50),
                ("highentropy", 1.10),
            })
            {
                string metricName = $"fresh-process-memory/{distribution}/{candidate}-vs-dense";
                E1MetricSummary? summary = metrics.SingleOrDefault(metric => string.Equals(metric.Name, metricName, StringComparison.Ordinal));
                if (summary?.BootstrapUpper95 is not double upper)
                {
                    available = false;
                    observations.Add($"{distribution}=inconclusive");
                    continue;
                }

                passes &= upper <= threshold;
                observations.Add($"{distribution} upper95={upper.ToString("F6", CultureInfo.InvariantCulture)} (limit {threshold.ToString("F2", CultureInfo.InvariantCulture)})");
            }

            criteria.Add(new E1CriterionAssessment(
                name,
                !available ? E1CriterionStatus.Inconclusive : passes ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                string.Join("; ", observations)));
        }

        private static void AddAdaptiveTimingCriterion(
            List<E1CriterionAssessment> criteria,
            E1Profile profile,
            E1MetricSummary[] metrics,
            string candidate)
        {
            string name = $"adaptive-timing-{candidate}";
            if (profile != E1Profile.Full)
            {
                criteria.Add(new E1CriterionAssessment(name, E1CriterionStatus.Inconclusive, "The CI timing profile is not decision-eligible."));
                return;
            }

            E1MetricSummary[] selected =
            [
                .. metrics.Where(metric => metric.Name.StartsWith("adaptive-vs-dense/", StringComparison.Ordinal) &&
                    metric.Name.EndsWith($"/{candidate}", StringComparison.Ordinal)),
            ];
            bool available = selected.Length == 4 && selected.All(metric => metric.BootstrapUpper95.HasValue);
            double maximumUpper = available ? selected.Max(metric => metric.BootstrapUpper95!.Value) : double.NaN;
            criteria.Add(new E1CriterionAssessment(
                name,
                !available ? E1CriterionStatus.Inconclusive : maximumUpper <= 1.15 ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                available
                    ? $"Maximum upper95 adaptive/dense ratio across random read, linear read, interior edits, and boundary edits is {maximumUpper.ToString("F6", CultureInfo.InvariantCulture)} (limit 1.15)."
                    : $"Expected four adaptive/dense timing summaries for {candidate}; found {selected.Length}."));
        }

        private static void AddSectionSideCriteria(
            List<E1CriterionAssessment> criteria,
            E1Profile profile,
            E1MetricSummary[] metrics,
            E1AmplificationSummary[] amplification,
            bool preferSide32)
        {
            string side = preferSide32 ? "side32" : "side16";
            if (profile != E1Profile.Full)
            {
                if (preferSide32)
                {
                    criteria.Add(new E1CriterionAssessment("side32-memory", E1CriterionStatus.Inconclusive, "The CI retained-memory profile is not decision-eligible."));
                }

                criteria.Add(new E1CriterionAssessment($"{side}-primary", E1CriterionStatus.Inconclusive, "The CI memory/timing profile is not decision-eligible."));
                criteria.Add(new E1CriterionAssessment($"{side}-amplification", E1CriterionStatus.Inconclusive, "The CI amplification profile is diagnostic only."));
                return;
            }

            if (preferSide32)
            {
                E1MetricSummary? memory = metrics.SingleOrDefault(metric => string.Equals(metric.Name, "fresh-process-memory/balanced/section-side", StringComparison.Ordinal));
                criteria.Add(new E1CriterionAssessment(
                    "side32-memory",
                    memory?.BootstrapUpper95 is not double memoryUpper
                        ? E1CriterionStatus.Inconclusive
                        : memoryUpper <= 0.80 ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                    memory?.BootstrapUpper95 is double observedMemoryUpper
                        ? $"Balanced upper95 side32/side16 retained-memory ratio is {observedMemoryUpper.ToString("F6", CultureInfo.InvariantCulture)} (limit 0.80)."
                        : "The balanced side32/side16 retained-memory interval is unavailable."));
            }

            string prefix = preferSide32 ? "section-side/" : "section-side-reciprocal/";
            string memoryName = preferSide32
                ? "fresh-process-memory/balanced/section-side"
                : "fresh-process-memory/balanced/section-side-reciprocal";
            string[] timingNames = ["read", "clustered-edit", "snapshot", "logical-projection"];
            E1MetricSummary?[] primary =
            [
                metrics.SingleOrDefault(metric => string.Equals(metric.Name, memoryName, StringComparison.Ordinal)),
                .. timingNames.Select(primaryName => metrics.SingleOrDefault(metric => string.Equals(metric.Name, prefix + primaryName, StringComparison.Ordinal))),
            ];
            bool primaryAvailable = primary.All(metric => metric?.BootstrapUpper95.HasValue == true && metric.MedianRatio.HasValue);
            if (preferSide32)
            {
                int strongWins = primaryAvailable ? primary.Count(metric => metric!.BootstrapUpper95!.Value <= 0.80) : 0;
                double maximumUpper = primaryAvailable ? primary.Max(metric => metric!.BootstrapUpper95!.Value) : double.NaN;
                criteria.Add(new E1CriterionAssessment(
                    "side32-primary",
                    !primaryAvailable ? E1CriterionStatus.Inconclusive : strongWins >= 3 && maximumUpper <= 1.15 ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                    primaryAvailable
                        ? $"{strongWins} of five retained-memory/read/edit/snapshot/projection upper95 side32/side16 ratios are <= 0.80; maximum upper95 is {maximumUpper.ToString("F6", CultureInfo.InvariantCulture)} (limit 1.15)."
                        : "One or more of the five grouped side32/side16 primary intervals is unavailable."));
            }
            else
            {
                double geometricMean = primaryAvailable
                    ? Math.Exp(primary.Average(metric => Math.Log(metric!.MedianRatio!.Value)))
                    : double.NaN;
                double maximumUpper = primaryAvailable ? primary.Max(metric => metric!.BootstrapUpper95!.Value) : double.NaN;
                criteria.Add(new E1CriterionAssessment(
                    "side16-primary",
                    !primaryAvailable ? E1CriterionStatus.Inconclusive : geometricMean <= 1.15 && maximumUpper <= 1.25 ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                    primaryAvailable
                        ? $"Equal-weight geometric mean of five median side16/side32 ratios is {geometricMean.ToString("F6", CultureInfo.InvariantCulture)} (limit 1.15); maximum upper95 is {maximumUpper.ToString("F6", CultureInfo.InvariantCulture)} (limit 1.25)."
                        : "One or more of the five grouped side16/side32 primary intervals is unavailable."));
            }

            (bool amplificationAvailable, double maximumAmplification) = MaximumAmplificationRatio(amplification, preferSide32);
            criteria.Add(new E1CriterionAssessment(
                $"{side}-amplification",
                !amplificationAvailable ? E1CriterionStatus.Inconclusive : maximumAmplification <= 2.0 ? E1CriterionStatus.Pass : E1CriterionStatus.Fail,
                amplificationAvailable
                    ? $"Maximum p95 candidate/reference ratio across canonical logical-projection bytes and unique remesh-halo samples is {maximumAmplification.ToString("F6", CultureInfo.InvariantCulture)} (limit 2.0)."
                    : "One or more paired p95 logical-byte or unique-halo observations is unavailable."));
        }

        private static (bool IsAvailable, double Maximum) MaximumAmplificationRatio(
            E1AmplificationSummary[] summaries,
            bool preferSide32)
        {
            string candidateLayout = preferSide32 ? "one-side32" : "eight-side16";
            string referenceLayout = preferSide32 ? "eight-side16" : "one-side32";
            List<double> ratios = [];
            foreach (string trace in new[] { SectionEditTraceKind.InteriorClusters.ToString(), SectionEditTraceKind.BoundaryClusters.ToString() })
            {
                E1AmplificationSummary? candidate = summaries.SingleOrDefault(summary => summary.Distribution is null && summary.Layout == candidateLayout && summary.Trace == trace);
                E1AmplificationSummary? reference = summaries.SingleOrDefault(summary => summary.Distribution is null && summary.Layout == referenceLayout && summary.Trace == trace);
                if (candidate is null || reference is null || reference.LogicalProjectionBytesP95 <= 0 || reference.UniqueHaloSamplesP95 <= 0)
                {
                    return (false, double.NaN);
                }

                ratios.Add(candidate.LogicalProjectionBytesP95 / (double)reference.LogicalProjectionBytesP95);
                ratios.Add(candidate.UniqueHaloSamplesP95 / (double)reference.UniqueHaloSamplesP95);
            }

            return (true, ratios.Max());
        }

        private static bool Passes(List<E1CriterionAssessment> criteria, string name)
        {
            return criteria.Single(criterion => string.Equals(criterion.Name, name, StringComparison.Ordinal)).Status == E1CriterionStatus.Pass;
        }
    }

    private sealed record E1ReportDocument(
        E1RunConfiguration Run,
        SectionObservationManifest Manifest,
        E1IndexingValidation IndexingValidation,
        E1PaletteBoundaryDiagnostic[] PaletteBoundaryDiagnostics,
        E1CorpusFingerprints CorpusFingerprints,
        E1MetricDefinition[] MetricDefinitions,
        E1MetricSummary[] MetricSummaries,
        E1AmplificationSummary[] AmplificationSummaries,
        E1MemoryReport Memory,
        E1DecisionAssessment Decision);

    private static double Median(double[] sorted)
    {
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] / 2.0) + (sorted[middle] / 2.0);
    }

    private static (double Lower, double Upper) BootstrapMedianInterval(double[] values, int resamples, ulong seed)
    {
        double[] samples = new double[resamples];
        double[] bootstrap = new double[values.Length];
        ulong state = seed;
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
        {
            for (int index = 0; index < bootstrap.Length; index++)
            {
                bootstrap[index] = values[checked((int)(NextRandom(ref state) % (uint)values.Length))];
            }

            Array.Sort(bootstrap);
            samples[sampleIndex] = Median(bootstrap);
        }

        Array.Sort(samples);
        int lower = checked((int)Math.Floor((samples.Length - 1) * 0.025));
        int upper = checked((int)Math.Ceiling((samples.Length - 1) * 0.975));
        return (samples[lower], samples[upper]);
    }

    private static ulong NextRandom(ref ulong state)
    {
        state += CubeSeedStride;
        ulong value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
