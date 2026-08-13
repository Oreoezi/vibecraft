using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VibeCraft.Content;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

internal static class SectionAmplificationReport
{
    private const string WindowSemantics =
        "One natural batching window is one contiguous deterministic 4x4x4 edit cluster: up to SectionEqualVolumeFixture.EditsPerCluster (64) operations. The trace is applied sequentially, dirty sections are deduplicated only within each window, and payload metrics are sampled after that window. A partial final window is retained.";
    private const string PercentileDefinition =
        "P95 is the nearest-rank 95th percentile: sort the per-window values ascending and select rank ceil(0.95 * window count), using one-based ranks. Median is the middle value, or the arithmetic mean of the two middle values for an even window count.";
    private const string MetricDefinitions =
        "DirtySectionTouches counts distinct changed sections per window. LogicalValuesRepublished counts complete logical section values for those sections. KnownInMemoryPayloadBytes counts their owned adaptive scalar/array payload after that window; it is not retained process memory, storage, save, serialized, compressed, or wire bytes. GrossHaloSamples sums each dirty section's (side+2)^3 remesh input. UniqueDeduplicatedHaloSamples counts distinct world-space halo sample coordinates per window. Aggregates sum the independently measured windows and never combine unlike units.";
    private const string EvidenceUse =
        "Observational only: these measurements do not select a section side, container, compatibility format, or user-world format.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static int Run(string[] args)
    {
        Options options = Options.Parse(args);
        SectionFixtureKind[] fixtures = options.AllFixtures
            ?
            [
                SectionFixtureKind.UniformAir,
                SectionFixtureKind.UniformStone,
                SectionFixtureKind.Layered,
                SectionFixtureKind.Mixed,
                SectionFixtureKind.HighEntropy,
            ]
            : [SectionBenchmarkSupport.ParseFixture(options.Fixture)];
        List<AmplificationObservation> observations = [];
        foreach (SectionFixtureKind fixture in fixtures)
        {
            BlockStateId[] canonical = SectionEqualVolumeFixture.CreateCanonicalCube(fixture, options.Seed);
            foreach (SectionEditTraceKind traceKind in Enum.GetValues<SectionEditTraceKind>())
            {
                SectionEdit[] trace = SectionEqualVolumeFixture.CreateEditTrace(
                    canonical,
                    traceKind,
                    options.Seed,
                    options.ClusterCount);
                foreach (SectionEqualVolumeLayout layout in Enum.GetValues<SectionEqualVolumeLayout>())
                {
                    observations.Add(Observe(canonical, trace, fixture, traceKind, layout));
                }
            }
        }

        SectionObservationManifest manifest = SectionObservationManifest.Capture(
            options.Seed,
            Environment.CommandLine,
            $"Release; deterministic amplification report; clusters={options.ClusterCount}");
        AmplificationReportDocument report = new(
            manifest,
            WindowSemantics,
            PercentileDefinition,
            MetricDefinitions,
            EvidenceUse,
            [.. observations]);
        WriteReport(report, ToMarkdown(report), options.OutputDirectory);
        return 0;
    }

    private static AmplificationObservation Observe(
        BlockStateId[] canonical,
        SectionEdit[] trace,
        SectionFixtureKind fixture,
        SectionEditTraceKind traceKind,
        SectionEqualVolumeLayout layout)
    {
        BlockStateId[] dense = (BlockStateId[])canonical.Clone();
        MutableSectionBlockStates[] sections = SectionEqualVolumeFixture.CreateSections(layout, canonical);
        int changed = 0;
        int unchanged = 0;
        int noOpIntents = 0;
        int existingStateChangeIntents = 0;
        int newStateChangeIntents = 0;
        List<AmplificationWindowMetrics> windows = [];
        for (int windowStart = 0; windowStart < trace.Length; windowStart += SectionEqualVolumeFixture.EditsPerCluster)
        {
            int windowLength = Math.Min(SectionEqualVolumeFixture.EditsPerCluster, trace.Length - windowStart);
            HashSet<int> dirtySections = [];
            for (int windowOffset = 0; windowOffset < windowLength; windowOffset++)
            {
                SectionEdit edit = trace[windowStart + windowOffset];
                switch (edit.Intent)
                {
                    case SectionEditIntent.NoOp: noOpIntents++; break;
                    case SectionEditIntent.ExistingStateChange: existingStateChangeIntents++; break;
                    case SectionEditIntent.NewStateChange: newStateChangeIntents++; break;
                    default: throw new InvalidOperationException($"Undefined edit intent {edit.Intent}.");
                }

                SectionWriteResult denseResult = SectionEqualVolumeFixture.SetDense(dense, edit);
                SectionWriteResult adaptiveResult = SectionEqualVolumeFixture.SetGlobal(sections, layout, edit);
                if (denseResult != adaptiveResult)
                {
                    throw new InvalidOperationException($"Amplification trace diverged for {fixture}/{traceKind}/{layout} at {edit.GlobalIndex}.");
                }

                if (adaptiveResult == SectionWriteResult.Changed)
                {
                    changed++;
                    _ = dirtySections.Add(SectionEqualVolumeFixture.GetSectionIndexForGlobal(layout, edit.GlobalIndex));
                }
                else
                {
                    unchanged++;
                }
            }

            windows.Add(MeasureWindow(sections, layout, dirtySections));
        }

        SectionBenchmarkSupport.ValidateEqualWorld(sections, layout, dense);
        return new AmplificationObservation(
            fixture,
            traceKind,
            layout,
            trace.Length,
            windows.Count,
            windows.Count == 0 ? 0 : trace.Length - ((windows.Count - 1) * SectionEqualVolumeFixture.EditsPerCluster),
            changed,
            unchanged,
            noOpIntents,
            existingStateChangeIntents,
            newStateChangeIntents,
            AmplificationMetricDistribution.Create(windows, static window => window.DirtySectionTouches),
            AmplificationMetricDistribution.Create(windows, static window => window.LogicalValuesRepublished),
            AmplificationMetricDistribution.Create(windows, static window => window.KnownInMemoryPayloadBytes),
            AmplificationMetricDistribution.Create(windows, static window => window.GrossHaloSamples),
            AmplificationMetricDistribution.Create(windows, static window => window.UniqueDeduplicatedHaloSamples),
            SectionBenchmarkSupport.Checksum(dense));
    }

    private static AmplificationWindowMetrics MeasureWindow(
        MutableSectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        HashSet<int> dirtySections)
    {
        int side = layout == SectionEqualVolumeLayout.OneSide32 ? 32 : 16;
        long logicalValuesRepublished = checked((long)dirtySections.Count * side * side * side);
        long knownPayloadBytes = 0;
        foreach (int dirtySection in dirtySections)
        {
            knownPayloadBytes = checked(knownPayloadBytes + sections[dirtySection].GetStorageMetrics().KnownPayloadBytes);
        }

        long grossHaloSamples = checked((long)dirtySections.Count * (side + 2) * (side + 2) * (side + 2));
        long uniqueHaloSamples = CountUniqueHaloSamples(layout, side, dirtySections);
        return new AmplificationWindowMetrics(
            dirtySections.Count,
            logicalValuesRepublished,
            knownPayloadBytes,
            grossHaloSamples,
            uniqueHaloSamples);
    }

    private static long CountUniqueHaloSamples(
        SectionEqualVolumeLayout layout,
        int side,
        HashSet<int> dirtySections)
    {
        HashSet<(int X, int Y, int Z)> unique = [];
        foreach (int sectionIndex in dirtySections)
        {
            SectionEqualVolumeFixture.GetSectionCoordinates(
                layout,
                sectionIndex,
                out int sectionX,
                out int sectionY,
                out int sectionZ);
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

    private static string ToMarkdown(AmplificationReportDocument report)
    {
        StringBuilder text = new();
        _ = text.Append("# ").Append(report.Manifest.FixtureId).AppendLine(" amplification observations");
        _ = text.AppendLine().AppendLine(report.Manifest.ClassificationReason).AppendLine();
        _ = text.AppendLine(report.EvidenceUse).AppendLine();
        _ = text.AppendLine("## Window and distribution semantics").AppendLine();
        _ = text.AppendLine(report.WindowSemantics).AppendLine();
        _ = text.AppendLine(report.PercentileDefinition).AppendLine();
        _ = text.AppendLine(report.MetricDefinitions).AppendLine();
        foreach (AmplificationObservation observation in report.Observations)
        {
            _ = text.AppendLine().Append("## ").Append(observation.Fixture).Append(" / ")
                .Append(observation.Trace).Append(" / ").AppendLine(observation.Layout.ToString()).AppendLine();
            _ = text.Append("Operations: ").Append(observation.OperationCount.ToString(CultureInfo.InvariantCulture))
                .Append("; windows: ").Append(observation.WindowCount.ToString(CultureInfo.InvariantCulture))
                .Append("; final-window operations: ").Append(observation.FinalWindowOperationCount.ToString(CultureInfo.InvariantCulture))
                .Append("; results changed/no-op: ").Append(observation.ChangedOperationCount.ToString(CultureInfo.InvariantCulture))
                .Append('/').Append(observation.NoOpOperationCount.ToString(CultureInfo.InvariantCulture)).AppendLine(".");
            _ = text.Append("Operation intents no-op/existing-state-change/new-state-change: ")
                .Append(observation.NoOpIntentCount.ToString(CultureInfo.InvariantCulture)).Append('/')
                .Append(observation.ExistingStateChangeIntentCount.ToString(CultureInfo.InvariantCulture)).Append('/')
                .Append(observation.NewStateChangeIntentCount.ToString(CultureInfo.InvariantCulture)).AppendLine(".");
            _ = text.Append("Final logical checksum: `")
                .Append(observation.FinalLogicalChecksum.ToString(CultureInfo.InvariantCulture)).AppendLine("`.").AppendLine();
            _ = text.AppendLine("| Metric (unit) | Aggregate | Min | Median | P95 | Max |");
            _ = text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
            AppendDistribution(text, "Dirty section touches (sections)", observation.DirtySectionTouches);
            AppendDistribution(text, "Logical values republished (values)", observation.LogicalValuesRepublished);
            AppendDistribution(text, "Known in-memory payload (bytes)", observation.KnownInMemoryPayloadBytes);
            AppendDistribution(text, "Gross halo samples (samples)", observation.GrossHaloSamples);
            AppendDistribution(text, "Unique deduplicated halo samples (samples)", observation.UniqueDeduplicatedHaloSamples);
        }

        _ = text.AppendLine().AppendLine("## Observational run manifest").AppendLine();
        _ = text.AppendLine("| Field | Value |").AppendLine("| --- | --- |");
        AppendRow(text, "Classification", report.Manifest.EvidenceClassification);
        AppendRow(text, "Reason", report.Manifest.ClassificationReason);
        AppendRow(text, "Commit / dirty", $"{report.Manifest.Commit} / {report.Manifest.WorkingTreeDirty}");
        AppendRow(text, "Source tree SHA-256", report.Manifest.SourceTreeSha256);
        AppendRow(text, "Working-tree diff SHA-256", report.Manifest.WorkingTreeDiffSha256);
        AppendRow(text, "Source identity method", report.Manifest.SourceIdentityMethod);
        AppendRow(text, "Benchmark assembly SHA-256", report.Manifest.BenchmarkAssemblySha256);
        AppendRow(text, "Benchmark executable SHA-256", report.Manifest.BenchmarkExecutableSha256);
        AppendRow(text, "Fixture / seed", $"{report.Manifest.FixtureId} / 0x{report.Manifest.Seed:X16}");
        AppendRow(
            text,
            "Runtime / SDK / assembly configuration",
            $"{report.Manifest.Runtime} / {report.Manifest.Sdk} / {report.Manifest.AssemblyConfiguration}");
        AppendRow(text, "OS / architecture", $"{report.Manifest.OperatingSystem} / {report.Manifest.ProcessArchitecture}");
        AppendRow(text, "CPU / logical processors", $"{report.Manifest.Cpu} / {report.Manifest.LogicalProcessorCount}");
        AppendRow(text, "Process affinity", report.Manifest.ProcessAffinity);
        AppendRow(
            text,
            "RAM total / available / managed budget",
            $"{FormatBytes(report.Manifest.TotalPhysicalMemoryBytes)} / {FormatBytes(report.Manifest.AvailablePhysicalMemoryBytes)} / {report.Manifest.ManagedMemoryBudgetBytes.ToString(CultureInfo.InvariantCulture)} ({report.Manifest.MemoryDiscovery})");
        AppendRow(text, "Machine model / power mode", $"{report.Manifest.MachineModel} / {report.Manifest.PowerMode}");
        AppendRow(text, "GC", $"Server={report.Manifest.ServerGc}; Latency={report.Manifest.GcLatencyMode}");
        AppendRow(text, "Command / invocation context", $"{report.Manifest.Command} / {report.Manifest.InvocationContext}");
        AppendRow(text, "Timestamp", report.Manifest.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendRow(text, "G0", $"{report.Manifest.G0FixtureId}: {report.Manifest.G0Status}");
        return text.ToString().TrimEnd();
    }

    private static void AppendDistribution(
        StringBuilder text,
        string metric,
        AmplificationMetricDistribution distribution)
    {
        _ = text.Append("| ").Append(metric).Append(" | ")
            .Append(distribution.Aggregate.ToString(CultureInfo.InvariantCulture)).Append(" | ")
            .Append(distribution.Minimum.ToString(CultureInfo.InvariantCulture)).Append(" | ")
            .Append(distribution.Median.ToString(CultureInfo.InvariantCulture)).Append(" | ")
            .Append(distribution.P95NearestRank.ToString(CultureInfo.InvariantCulture)).Append(" | ")
            .Append(distribution.Maximum.ToString(CultureInfo.InvariantCulture)).AppendLine(" |");
    }

    private static void AppendRow(StringBuilder text, string field, string value)
    {
        _ = text.Append("| ").Append(Escape(field)).Append(" | ").Append(Escape(value)).AppendLine(" |");
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
    }

    private static string FormatBytes(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }

    private static void WriteReport(
        AmplificationReportDocument report,
        string markdown,
        string? outputDirectory)
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
        File.WriteAllText(Path.Combine(outputDirectory, "section-amplification.json"), json + Environment.NewLine, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, "section-amplification.md"), markdown + Environment.NewLine, Encoding.UTF8);
        Console.WriteLine($"Wrote observational evidence to {Path.GetFullPath(outputDirectory)}");
    }

    private sealed record AmplificationReportDocument(
        SectionObservationManifest Manifest,
        string WindowSemantics,
        string PercentileDefinition,
        string MetricDefinitions,
        string EvidenceUse,
        AmplificationObservation[] Observations);

    private sealed record Options(
        string Fixture,
        bool AllFixtures,
        ulong Seed,
        int ClusterCount,
        string? OutputDirectory)
    {
        internal static Options Parse(string[] args)
        {
            string fixture = "Mixed";
            bool allFixtures = true;
            ulong seed = SectionCandidateFixture.DefaultSeed;
            int clusterCount = SectionEqualVolumeFixture.DefaultClusterCount;
            string? output = null;
            bool smoke = false;
            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                if (option == "--all-fixtures")
                {
                    allFixtures = true;
                    continue;
                }

                if (option == "--smoke")
                {
                    smoke = true;
                    continue;
                }

                string value = index + 1 < args.Length
                    ? args[++index]
                    : throw new ArgumentException($"Missing value after {option}.", nameof(args));
                switch (option)
                {
                    case "--fixture": fixture = value; allFixtures = false; break;
                    case "--seed":
                        seed = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? ulong.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                            : ulong.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--clusters": clusterCount = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--output": output = value; break;
                    default: throw new ArgumentException($"Unknown amplification option: {option}", nameof(args));
                }
            }

            if (smoke)
            {
                allFixtures = false;
                fixture = "Mixed";
                clusterCount = Math.Min(clusterCount, 8);
            }

            if (!allFixtures)
            {
                _ = SectionBenchmarkSupport.ParseFixture(fixture);
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clusterCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(clusterCount, 4096);
            return new Options(fixture, allFixtures, seed, clusterCount, output);
        }
    }
}

internal sealed record AmplificationObservation(
    SectionFixtureKind Fixture,
    SectionEditTraceKind Trace,
    SectionEqualVolumeLayout Layout,
    int OperationCount,
    int WindowCount,
    int FinalWindowOperationCount,
    int ChangedOperationCount,
    int NoOpOperationCount,
    int NoOpIntentCount,
    int ExistingStateChangeIntentCount,
    int NewStateChangeIntentCount,
    AmplificationMetricDistribution DirtySectionTouches,
    AmplificationMetricDistribution LogicalValuesRepublished,
    AmplificationMetricDistribution KnownInMemoryPayloadBytes,
    AmplificationMetricDistribution GrossHaloSamples,
    AmplificationMetricDistribution UniqueDeduplicatedHaloSamples,
    ulong FinalLogicalChecksum);

internal readonly record struct AmplificationWindowMetrics(
    long DirtySectionTouches,
    long LogicalValuesRepublished,
    long KnownInMemoryPayloadBytes,
    long GrossHaloSamples,
    long UniqueDeduplicatedHaloSamples);

internal sealed record AmplificationMetricDistribution(
    long Aggregate,
    long Minimum,
    decimal Median,
    long P95NearestRank,
    long Maximum)
{
    internal static AmplificationMetricDistribution Create(
        IReadOnlyList<AmplificationWindowMetrics> windows,
        Func<AmplificationWindowMetrics, long> selector)
    {
        if (windows.Count == 0)
        {
            return new AmplificationMetricDistribution(0, 0, 0, 0, 0);
        }

        long aggregate = 0;
        long[] sorted = new long[windows.Count];
        for (int index = 0; index < windows.Count; index++)
        {
            long value = selector(windows[index]);
            if (value < 0)
            {
                throw new InvalidOperationException("Amplification window metrics cannot be negative.");
            }

            aggregate = checked(aggregate + value);
            sorted[index] = value;
        }

        Array.Sort(sorted);
        int middle = sorted.Length / 2;
        decimal median = (sorted.Length & 1) == 1
            ? sorted[middle]
            : ((decimal)sorted[middle - 1] + sorted[middle]) / 2m;
        int p95Index = checked(((95 * sorted.Length) + 99) / 100) - 1;
        return new AmplificationMetricDistribution(
            aggregate,
            sorted[0],
            median,
            sorted[p95Index],
            sorted[^1]);
    }
}
