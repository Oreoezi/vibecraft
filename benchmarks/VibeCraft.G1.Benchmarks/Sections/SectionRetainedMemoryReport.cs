using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VibeCraft.Content;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

internal enum RetainedMemoryMode : byte
{
    DenseCanonical = 0,
    OneSide32Adaptive = 1,
    EightSide16Adaptive = 2,
}

internal static class SectionRetainedMemoryReport
{
    private const long DefaultMaxBytes = 512L * 1024L * 1024L;
    private static readonly TimeSpan ChildProcessTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static int RunParent(string[] args)
    {
        ParentOptions options = ParentOptions.Parse(args);
        SectionFixtureKind fixture = SectionBenchmarkSupport.ParseFixture(options.Fixture);
        RetainedMemoryMode[] modes = Enum.GetValues<RetainedMemoryMode>();
        long availableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        foreach (RetainedMemoryMode mode in modes)
        {
            _ = RetainedMemorySafety.Validate(mode, options.CubeCount, options.MaxBytes, availableBytes);
        }

        List<RetainedMemoryTrial> trials = [];
        foreach (RetainedMemoryMode mode in modes)
        {
            for (int trialIndex = 0; trialIndex < options.TrialCount; trialIndex++)
            {
                RetainedMemoryTrial trial = LaunchTrial(mode, fixture, options, trialIndex);
                trials.Add(trial);
            }
        }

        int minimumValidTrials = Math.Min(3, options.TrialCount);
        RetainedMemoryModeReport[] modeReports =
        [
            .. modes.Select(mode => AggregateMode(
                mode,
                [.. trials.Where(trial => trial.Mode == mode)],
                minimumValidTrials)),
        ];
        SectionObservationManifest manifest = SectionObservationManifest.Capture(
            options.Seed,
            Environment.CommandLine,
            "fresh-process retained-memory parent");
        RetainedMemoryReportDocument report = new(
            manifest,
            options.Fixture,
            options.CubeCount,
            options.TrialCount,
            minimumValidTrials,
            options.MaxBytes,
            availableBytes,
            $"Each mode/trial runs in a new child process. The child warms once, performs full compacting collections, retains an identical set of canonical 32-cubed logical cubes as dense arrays or derived adaptive sections, and reports the signed GC.GetTotalMemory(true) delta. Negative deltas are invalid/noisy and are never clamped. A mode is conclusive only with at least {minimumValidTrials} valid sample(s), min(3, requested trials).",
            modeReports);
        WriteReport(
            report,
            ToMarkdown(report),
            options.OutputDirectory,
            "section-retained-memory");
        return modeReports.All(mode => mode.IsConclusive) ? 0 : 2;
    }

    internal static int RunChild(string[] args)
    {
        ChildOptions options = ChildOptions.Parse(args);
        SectionFixtureKind fixture = SectionBenchmarkSupport.ParseFixture(options.Fixture);
        long availableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        RetainedMemorySafety safety = RetainedMemorySafety.Validate(
            options.Mode,
            options.CubeCount,
            options.MaxBytes,
            availableBytes);

        Warm(options.Mode, fixture, options.Seed);
        Collect();
        long before = GC.GetTotalMemory(forceFullCollection: true);
        RetainedCorpus corpus = BuildCorpus(options.Mode, fixture, options.Seed, options.CubeCount);
        long after = GC.GetTotalMemory(forceFullCollection: true);
        long delta = checked(after - before);
        bool valid = delta >= 0;
        RetainedMemoryTrial trial = new(
            options.Mode,
            options.TrialIndex,
            Environment.ProcessId,
            options.CubeCount,
            safety.ConservativeEstimatedBytes,
            corpus.KnownInMemoryPayloadBytes,
            corpus.LogicalChecksum,
            before,
            after,
            delta,
            valid,
            valid ? null : "GC noise produced a negative retained-memory delta; this trial is excluded from statistics.");
        GC.KeepAlive(corpus.Roots);
        Console.WriteLine(JsonSerializer.Serialize(trial, JsonOptions));
        return 0;
    }

    private static RetainedMemoryTrial LaunchTrial(
        RetainedMemoryMode mode,
        SectionFixtureKind fixture,
        ParentOptions options,
        int trialIndex)
    {
        ProcessStartInfo startInfo = CreateSelfStartInfo();
        AddArgument(startInfo, "--section-memory-trial");
        AddArgument(startInfo, "--mode", mode.ToString());
        AddArgument(startInfo, "--fixture", fixture.ToString());
        AddArgument(startInfo, "--seed", $"0x{options.Seed:X16}");
        AddArgument(startInfo, "--cubes", options.CubeCount.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--trial-index", trialIndex.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--max-bytes", options.MaxBytes.ToString(CultureInfo.InvariantCulture));
        ProcessExecutionResult result = SectionBenchmarkSupport.RunProcess(startInfo, ChildProcessTimeout);
        if (result.TimedOut)
        {
            throw new TimeoutException(
                $"Retained-memory child timed out after {ChildProcessTimeout} for {mode}, trial {trialIndex}. "
                + $"stdout: {ForError(result.StandardOutput)} stderr: {ForError(result.StandardError)}");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Retained-memory child failed for {mode}, trial {trialIndex}, exit {result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}. "
                + $"stdout: {ForError(result.StandardOutput)} stderr: {ForError(result.StandardError)}");
        }

        RetainedMemoryTrial? trial = JsonSerializer.Deserialize<RetainedMemoryTrial>(result.StandardOutput, JsonOptions);
        return trial ?? throw new InvalidDataException($"Retained-memory child emitted no structured trial for {mode}, trial {trialIndex}.");
    }

    private static string ForError(string value)
    {
        const int maximumLength = 4_096;
        string normalized = string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : $"{normalized[..maximumLength]}… <truncated>";
    }

    private static ProcessStartInfo CreateSelfStartInfo()
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current benchmark executable path is unavailable.");
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        if (string.Equals(
            Path.GetFileNameWithoutExtension(executable),
            "dotnet",
            StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(SectionRetainedMemoryReport).Assembly.Location);
        }

        return startInfo;
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string? value = null)
    {
        startInfo.ArgumentList.Add(name);
        if (value is not null)
        {
            startInfo.ArgumentList.Add(value);
        }
    }

    private static RetainedCorpus BuildCorpus(
        RetainedMemoryMode mode,
        SectionFixtureKind fixture,
        ulong seed,
        int cubeCount)
    {
        object[] roots = new object[cubeCount];
        long knownPayloadBytes = 0;
        ulong checksum = 0xCBF29CE484222325UL;
        for (int cubeIndex = 0; cubeIndex < cubeCount; cubeIndex++)
        {
            ulong cubeSeed = unchecked(seed + ((ulong)cubeIndex * 0x9E3779B97F4A7C15UL));
            WorldStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(fixture, cubeSeed);
            checksum = unchecked((checksum * 31UL) ^ SectionBenchmarkSupport.Checksum(canonical));
            if (mode == RetainedMemoryMode.DenseCanonical)
            {
                roots[cubeIndex] = canonical;
                knownPayloadBytes = checked(knownPayloadBytes + ((long)canonical.Length * sizeof(uint)));
                continue;
            }

            SectionEqualVolumeLayout layout = mode == RetainedMemoryMode.OneSide32Adaptive
                ? SectionEqualVolumeLayout.OneSide32
                : SectionEqualVolumeLayout.EightSide16;
            MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
            SectionBenchmarkSupport.ValidateEqualWorld(sections, layout, canonical);
            roots[cubeIndex] = sections;
            for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                knownPayloadBytes = checked(knownPayloadBytes + sections[sectionIndex].GetStorageMetrics().KnownPayloadBytes);
            }
        }

        return new RetainedCorpus(roots, knownPayloadBytes, checksum);
    }

    private static void Warm(RetainedMemoryMode mode, SectionFixtureKind fixture, ulong seed)
    {
        RetainedCorpus warm = BuildCorpus(mode, fixture, seed, 1);
        GC.KeepAlive(warm.Roots);
    }

    private static void Collect()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private static RetainedMemoryModeReport AggregateMode(
        RetainedMemoryMode mode,
        RetainedMemoryTrial[] trials,
        int minimumValidTrials)
    {
        long[] valid = [.. trials.Where(trial => trial.IsValid).Select(trial => trial.RetainedBytes).Order()];
        if (valid.Length == 0)
        {
            return new RetainedMemoryModeReport(
                mode,
                trials,
                0,
                trials.Length,
                minimumValidTrials,
                false,
                $"Inconclusive: 0 valid samples; at least {minimumValidTrials} required.",
                null,
                null,
                null,
                null,
                true);
        }

        double median = Median(valid);
        double[] deviations = [.. valid.Select(value => Math.Abs(value - median)).Order()];
        bool isConclusive = valid.Length >= minimumValidTrials;
        return new RetainedMemoryModeReport(
            mode,
            trials,
            valid.Length,
            trials.Length - valid.Length,
            minimumValidTrials,
            isConclusive,
            isConclusive
                ? null
                : $"Inconclusive: {valid.Length} valid sample(s); at least {minimumValidTrials} required.",
            valid[0],
            median,
            valid[^1],
            Median(deviations),
            trials.Length != valid.Length);
    }

    private static double Median(long[] sorted)
    {
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] / 2.0) + (sorted[middle] / 2.0);
    }

    private static double Median(double[] sorted)
    {
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] / 2.0) + (sorted[middle] / 2.0);
    }

    private static string ToMarkdown(RetainedMemoryReportDocument report)
    {
        StringBuilder text = new();
        _ = text.Append("# ").Append(report.Manifest.FixtureId).AppendLine(" retained-memory observations");
        _ = text.AppendLine();
        _ = text.AppendLine(report.Manifest.ClassificationReason);
        _ = text.AppendLine();
        _ = text.AppendLine("Each row retains identical canonical 32³ logical cubes. Known payload means owned in-memory scalar/array elements only; it is not serialized, storage, or wire bytes.");
        _ = text.AppendLine();
        _ = text.Append("A mode requires at least ")
            .Append(report.MinimumValidTrialsPerMode.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" valid sample(s) for this run; otherwise the report is inconclusive and exits nonzero.");
        _ = text.AppendLine();
        _ = text.AppendLine("| Mode | Status | Valid/noisy trials | Required | Known payload bytes | Retained min | Median | Max | MAD |");
        _ = text.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (RetainedMemoryModeReport mode in report.Modes)
        {
            long knownPayload = mode.Trials.Length == 0 ? 0 : mode.Trials[0].KnownInMemoryPayloadBytes;
            _ = text.Append("| ").Append(mode.Mode).Append(" | ")
                .Append(mode.IsConclusive ? "conclusive" : "inconclusive").Append(" | ")
                .Append(mode.ValidTrialCount.ToString(CultureInfo.InvariantCulture)).Append('/')
                .Append(mode.InvalidNoisyTrialCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(mode.MinimumValidTrialCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(knownPayload.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(FormatNullable(mode.MinimumRetainedBytes)).Append(" | ")
                .Append(FormatNullable(mode.MedianRetainedBytes)).Append(" | ")
                .Append(FormatNullable(mode.MaximumRetainedBytes)).Append(" | ")
                .Append(FormatNullable(mode.MedianAbsoluteDeviationBytes)).AppendLine(" |");
        }

        AppendManifest(text, report.Manifest);
        return text.ToString().TrimEnd();
    }

    private static string FormatNullable<T>(T? value)
        where T : struct, IFormattable
    {
        return value?.ToString(null, CultureInfo.InvariantCulture) ?? "invalid/noisy";
    }

    private static void AppendManifest(StringBuilder text, SectionObservationManifest manifest)
    {
        _ = text.AppendLine().AppendLine("## Observational run manifest").AppendLine();
        _ = text.AppendLine("| Field | Value |").AppendLine("| --- | --- |");
        AppendRow(text, "Classification", manifest.EvidenceClassification);
        AppendRow(text, "Reason", manifest.ClassificationReason);
        AppendRow(text, "Commit / dirty", $"{manifest.Commit} / {manifest.WorkingTreeDirty}");
        AppendRow(text, "Source tree SHA-256", manifest.SourceTreeSha256);
        AppendRow(text, "Working-tree diff SHA-256", manifest.WorkingTreeDiffSha256);
        AppendRow(text, "Source identity method", manifest.SourceIdentityMethod);
        AppendRow(text, "Benchmark assembly SHA-256", manifest.BenchmarkAssemblySha256);
        AppendRow(text, "Benchmark executable SHA-256", manifest.BenchmarkExecutableSha256);
        AppendRow(text, "Fixture / seed", $"{manifest.FixtureId} / 0x{manifest.Seed:X16}");
        AppendRow(text, "Runtime / SDK / assembly configuration", $"{manifest.Runtime} / {manifest.Sdk} / {manifest.AssemblyConfiguration}");
        AppendRow(text, "OS / architecture", $"{manifest.OperatingSystem} / {manifest.ProcessArchitecture}");
        AppendRow(text, "CPU / logical processors", $"{manifest.Cpu} / {manifest.LogicalProcessorCount}");
        AppendRow(text, "Process affinity", manifest.ProcessAffinity);
        AppendRow(
            text,
            "RAM total / available / managed budget",
            $"{FormatBytes(manifest.TotalPhysicalMemoryBytes)} / {FormatBytes(manifest.AvailablePhysicalMemoryBytes)} / {manifest.ManagedMemoryBudgetBytes.ToString(CultureInfo.InvariantCulture)} ({manifest.MemoryDiscovery})");
        AppendRow(text, "Machine model / power mode", $"{manifest.MachineModel} / {manifest.PowerMode}");
        AppendRow(text, "GC", $"Server={manifest.ServerGc}; Latency={manifest.GcLatencyMode}");
        AppendRow(text, "Command / invocation context", $"{manifest.Command} / {manifest.InvocationContext}");
        AppendRow(text, "Timestamp", manifest.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendRow(text, "G0", $"{manifest.G0FixtureId}: {manifest.G0Status}");
    }

    private static void AppendRow(StringBuilder text, string field, string value)
    {
        _ = text.Append("| ").Append(Escape(field)).Append(" | ").Append(Escape(value)).AppendLine(" |");
    }

    private static string FormatBytes(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    private static void WriteReport<T>(T report, string markdown, string? outputDirectory, string stem)
    {
        string json = JsonSerializer.Serialize(report, JsonOptions);
        if (outputDirectory is null)
        {
            Console.WriteLine(json);
            Console.WriteLine();
            Console.WriteLine(markdown);
            return;
        }

        _ = Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, $"{stem}.json"), json + Environment.NewLine, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, $"{stem}.md"), markdown + Environment.NewLine, Encoding.UTF8);
        Console.WriteLine($"Wrote observational evidence to {Path.GetFullPath(outputDirectory)}");
    }

    private readonly record struct RetainedCorpus(
        object[] Roots,
        long KnownInMemoryPayloadBytes,
        ulong LogicalChecksum);

    private sealed record RetainedMemoryReportDocument(
        SectionObservationManifest Manifest,
        string Fixture,
        int CanonicalCubeCount,
        int RequestedTrialsPerMode,
        int MinimumValidTrialsPerMode,
        long ExplicitMaxBytes,
        long AvailableMemoryBytes,
        string MeasurementMethod,
        RetainedMemoryModeReport[] Modes);

    private sealed record RetainedMemoryModeReport(
        RetainedMemoryMode Mode,
        RetainedMemoryTrial[] Trials,
        int ValidTrialCount,
        int InvalidNoisyTrialCount,
        int MinimumValidTrialCount,
        bool IsConclusive,
        string? InconclusiveReason,
        long? MinimumRetainedBytes,
        double? MedianRetainedBytes,
        long? MaximumRetainedBytes,
        double? MedianAbsoluteDeviationBytes,
        bool ContainsNoisyTrials);

    private sealed record ParentOptions(
        string Fixture,
        ulong Seed,
        int CubeCount,
        int TrialCount,
        long MaxBytes,
        string? OutputDirectory)
    {
        internal static ParentOptions Parse(string[] args)
        {
            string fixture = "Mixed";
            ulong seed = SectionCandidateFixture.DefaultSeed;
            int cubeCount = 64;
            int trialCount = 5;
            long maxBytes = DefaultMaxBytes;
            string? output = null;
            bool smoke = false;
            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                if (option == "--smoke")
                {
                    smoke = true;
                    continue;
                }

                string value = ReadValue(args, ref index, option);
                switch (option)
                {
                    case "--fixture": fixture = value; break;
                    case "--seed": seed = ParseSeed(value); break;
                    case "--cubes": cubeCount = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--trials": trialCount = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--max-bytes": maxBytes = long.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--output": output = value; break;
                    default: throw new ArgumentException($"Unknown retained-memory option: {option}", nameof(args));
                }
            }

            if (smoke)
            {
                cubeCount = Math.Min(cubeCount, 2);
                trialCount = Math.Min(trialCount, 2);
                maxBytes = Math.Min(maxBytes, 128L * 1024L * 1024L);
            }

            _ = SectionBenchmarkSupport.ParseFixture(fixture);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cubeCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trialCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(trialCount, 25);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
            return new ParentOptions(fixture, seed, cubeCount, trialCount, maxBytes, output);
        }
    }

    private sealed record ChildOptions(
        RetainedMemoryMode Mode,
        string Fixture,
        ulong Seed,
        int CubeCount,
        int TrialIndex,
        long MaxBytes)
    {
        internal static ChildOptions Parse(string[] args)
        {
            RetainedMemoryMode? mode = null;
            string fixture = "Mixed";
            ulong seed = SectionCandidateFixture.DefaultSeed;
            int cubeCount = 0;
            int trialIndex = 0;
            long maxBytes = 0;
            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                string value = ReadValue(args, ref index, option);
                switch (option)
                {
                    case "--mode":
                        mode = Enum.TryParse(value, ignoreCase: false, out RetainedMemoryMode parsedMode)
                            && Enum.IsDefined(parsedMode)
                            ? parsedMode
                            : throw new ArgumentException($"Unknown retained-memory child mode: {value}", nameof(args));
                        break;
                    case "--fixture": fixture = value; break;
                    case "--seed": seed = ParseSeed(value); break;
                    case "--cubes": cubeCount = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--trial-index": trialIndex = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--max-bytes": maxBytes = long.Parse(value, CultureInfo.InvariantCulture); break;
                    default: throw new ArgumentException($"Unknown retained-memory child option: {option}", nameof(args));
                }
            }

            return new ChildOptions(
                mode ?? throw new ArgumentException("A retained-memory child mode is required.", nameof(args)),
                fixture,
                seed,
                cubeCount,
                trialIndex,
                maxBytes);
        }
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        return index + 1 < args.Length
            ? args[++index]
            : throw new ArgumentException($"Missing value after {option}.", nameof(args));
    }

    private static ulong ParseSeed(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : ulong.Parse(value, CultureInfo.InvariantCulture);
    }
}

internal sealed record RetainedMemoryTrial(
    RetainedMemoryMode Mode,
    int TrialIndex,
    int ProcessId,
    int CanonicalCubeCount,
    long ConservativeEstimatedBytes,
    long KnownInMemoryPayloadBytes,
    ulong LogicalChecksum,
    long BaselineManagedBytes,
    long RetainedManagedBytes,
    long RetainedBytes,
    bool IsValid,
    string? InvalidOrNoisyReason);

internal sealed record RetainedMemorySafety(
    RetainedMemoryMode Mode,
    int CanonicalCubeCount,
    long ConservativeEstimatedBytes,
    long ExplicitMaxBytes,
    long AvailableMemoryBytes)
{
    internal static RetainedMemorySafety Validate(
        RetainedMemoryMode mode,
        int cubeCount,
        long maxBytes,
        long availableBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cubeCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        long perCubeBytes = mode switch
        {
            RetainedMemoryMode.DenseCanonical => 192L * 1024L,
            RetainedMemoryMode.OneSide32Adaptive => 512L * 1024L,
            RetainedMemoryMode.EightSide16Adaptive => 768L * 1024L,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The retained-memory mode is undefined."),
        };
        long estimate;
        try
        {
            estimate = checked(perCubeBytes * cubeCount);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("Retained-memory preflight overflowed before allocation; the request is refused.", exception);
        }

        if (estimate > maxBytes)
        {
            throw new InvalidOperationException(
                $"Retained-memory request refused before allocation: conservative estimate {estimate} exceeds explicit --max-bytes {maxBytes} for {mode}.");
        }

        long availableLimit = availableBytes > 0 ? availableBytes / 2 : long.MaxValue;
        return estimate <= availableLimit
            ? new RetainedMemorySafety(mode, cubeCount, estimate, maxBytes, availableBytes)
            : throw new InvalidOperationException(
                $"Retained-memory request refused before allocation: conservative estimate {estimate} exceeds half of available memory {availableBytes} for {mode}.");
    }
}
