using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VibeCraft.Content;
using VibeCraft.WorldModel.Sections;

namespace VibeCraft.G1.Benchmarks.Sections;

internal static class SectionBenchmarkSupport
{
    internal const int RandomTraceLength = 65_536;
    private static readonly TimeSpan ProcessTerminationTimeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static int _manifestEmitted;

    internal static SectionFixtureKind ParseFixture(string value)
    {
        return Enum.TryParse(value, ignoreCase: false, out SectionFixtureKind kind)
            && Enum.IsDefined(kind)
            && kind != SectionFixtureKind.PaletteBoundary
            ? kind
            : throw new ArgumentException($"Unknown named section fixture: {value}", nameof(value));
    }

    internal static SectionEqualVolumeLayout ParseLayout(string value)
    {
        return Enum.TryParse(value, ignoreCase: false, out SectionEqualVolumeLayout layout)
            && Enum.IsDefined(layout)
            ? layout
            : throw new ArgumentException($"Unknown equal-volume layout: {value}", nameof(value));
    }

    internal static SectionEditTraceKind ParseTrace(string value)
    {
        return Enum.TryParse(value, ignoreCase: false, out SectionEditTraceKind trace)
            && Enum.IsDefined(trace)
            ? trace
            : throw new ArgumentException($"Unknown section edit trace: {value}", nameof(value));
    }

    internal static int[] CreateRandomTrace(int count, int range, ulong seed)
    {
        int[] trace = new int[count];
        ulong state = seed;
        for (int index = 0; index < trace.Length; index++)
        {
            trace[index] = checked((int)(Next(ref state) % (uint)range));
        }

        return trace;
    }

    internal static ulong Checksum(ReadOnlySpan<BlockStateId> states)
    {
        ulong checksum = 0xCBF29CE484222325UL;
        foreach (BlockStateId state in states)
        {
            checksum = unchecked((checksum ^ state.Value) * 0x100000001B3UL);
        }

        return checksum;
    }

    internal static ulong AddEditChecksum(
        ulong checksum,
        SectionEdit edit,
        SectionWriteResult result)
    {
        checksum = unchecked((checksum ^ checked((uint)edit.GlobalIndex)) * 0x100000001B3UL);
        checksum = unchecked((checksum ^ edit.State.Value) * 0x100000001B3UL);
        return unchecked((checksum ^ (uint)result) * 0x100000001B3UL);
    }

    internal static BlockStateId[][] CreateSide16Scratch()
    {
        BlockStateId[][] scratch = new BlockStateId[8][];
        for (int index = 0; index < scratch.Length; index++)
        {
            scratch[index] = new BlockStateId[16 * 16 * 16];
        }

        return scratch;
    }

    internal static void ValidateEqualWorld(
        IReadOnlySectionBlockStates[] sections,
        SectionEqualVolumeLayout layout,
        ReadOnlySpan<BlockStateId> canonical)
    {
        if (canonical.Length != SectionEqualVolumeFixture.CubeVolume)
        {
            throw new InvalidOperationException("The benchmark canonical cube has the wrong volume.");
        }

        for (int index = 0; index < canonical.Length; index++)
        {
            if (!SectionEqualVolumeFixture.GetGlobal(sections, layout, index).Equals(canonical[index]))
            {
                throw new InvalidOperationException($"Global read mismatch for {layout} at canonical index {index}.");
            }
        }

        BlockStateId[] projection = new BlockStateId[canonical.Length];
        SectionEqualVolumeFixture.CopyToCanonical(sections, layout, projection, CreateSide16Scratch());
        if (!projection.AsSpan().SequenceEqual(canonical))
        {
            throw new InvalidOperationException($"Global CopyTo mismatch for {layout}.");
        }
    }

    internal static void EmitObservationManifestOnce(ulong seed, string invocationContext)
    {
        if (Interlocked.Exchange(ref _manifestEmitted, 1) != 0)
        {
            return;
        }

        SectionObservationManifest manifest = SectionObservationManifest.Capture(
            seed,
            Environment.CommandLine,
            invocationContext);
        Console.WriteLine("SECTION_OBSERVATION_MANIFEST_BEGIN");
        Console.WriteLine(JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine("SECTION_OBSERVATION_MANIFEST_END");
    }

    internal static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    internal static ProcessExecutionResult RunProcess(
        ProcessStartInfo startInfo,
        TimeSpan timeout)
    {
        return RunProcessAsync(startInfo, timeout).GetAwaiter().GetResult();
    }

    private static async Task<ProcessExecutionResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        bool timedOut = false;
        string? terminationError = null;
        using (CancellationTokenSource timeoutSource = new(timeout))
        {
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                timedOut = true;
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
                {
                    terminationError = exception.Message;
                }

                using CancellationTokenSource terminationSource = new(ProcessTerminationTimeout);
                try
                {
                    await process.WaitForExitAsync(terminationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (terminationSource.IsCancellationRequested)
                {
                    terminationError = terminationError is null
                        ? "The process tree did not terminate within the bounded kill grace period."
                        : $"{terminationError} The process tree did not terminate within the bounded kill grace period.";
                }
            }
        }

        string stdout = await ReadCapturedStream(stdoutTask).ConfigureAwait(false);
        string stderr = await ReadCapturedStream(stderrTask).ConfigureAwait(false);
        if (terminationError is not null)
        {
            stderr = string.IsNullOrWhiteSpace(stderr)
                ? terminationError
                : $"{stderr.TrimEnd()}{Environment.NewLine}{terminationError}";
        }

        int? exitCode = process.HasExited ? process.ExitCode : null;
        return new ProcessExecutionResult(exitCode, timedOut, stdout, stderr);
    }

    private static async Task<string> ReadCapturedStream(Task<string> streamTask)
    {
        try
        {
            return await streamTask.WaitAsync(ProcessTerminationTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return "<captured stream did not close within the bounded drain period>";
        }
    }
}

internal sealed record SectionObservationManifest(
    string EvidenceClassification,
    string ClassificationReason,
    string Commit,
    bool WorkingTreeDirty,
    string SourceTreeSha256,
    string WorkingTreeDiffSha256,
    string SourceIdentityMethod,
    string BenchmarkAssemblySha256,
    string BenchmarkExecutableSha256,
    string FixtureId,
    ulong Seed,
    string Runtime,
    string Sdk,
    string AssemblyConfiguration,
    string OperatingSystem,
    string ProcessArchitecture,
    string Cpu,
    int LogicalProcessorCount,
    string ProcessAffinity,
    long? TotalPhysicalMemoryBytes,
    long? AvailablePhysicalMemoryBytes,
    long ManagedMemoryBudgetBytes,
    string MemoryDiscovery,
    string MachineModel,
    string PowerMode,
    bool ServerGc,
    string GcLatencyMode,
    string Command,
    string InvocationContext,
    DateTimeOffset TimestampUtc,
    string G0FixtureId,
    string G0Status)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    // Existing report renderers consume this name; its value now comes from
    // AssemblyConfigurationAttribute rather than a caller-provided claim.
    internal string Configuration => AssemblyConfiguration;

    internal static SectionObservationManifest Capture(
        ulong seed,
        string command,
        string invocationContext)
    {
        string? repositoryRoot = RunProcess("git", ["rev-parse", "--show-toplevel"]);
        bool dirty = GetGitDirtyState();
        SourceIdentity sourceIdentity = CaptureSourceIdentity(repositoryRoot);
        Assembly assembly = typeof(SectionObservationManifest).Assembly;
        string assemblyPath = assembly.Location;
        string? executablePath = Environment.ProcessPath;
        HostMemory memory = CaptureHostMemory();
        const string g0Status = "PROVISIONAL — owner acceptance required";
        string reason = dirty
            ? "Observational only: the working tree is dirty and G0 remains provisional."
            : "Observational only: G0 remains provisional and owner acceptance is absent.";
        return new SectionObservationManifest(
            "observational",
            reason,
            RunProcess("git", ["rev-parse", "HEAD"]) ?? "unrecorded-working-tree",
            dirty,
            sourceIdentity.SourceTreeSha256,
            sourceIdentity.WorkingTreeDiffSha256,
            sourceIdentity.Method,
            ComputeFileSha256(assemblyPath),
            ComputeFileSha256(executablePath),
            SectionCandidateFixture.FixtureId,
            seed,
            RuntimeInformation.FrameworkDescription,
            GetSdkVersion(),
            assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown",
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            GetCpuDescription(),
            Environment.ProcessorCount,
            GetProcessAffinity(),
            memory.TotalBytes,
            memory.AvailableBytes,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            memory.Discovery,
            GetMachineModel(),
            GetPowerMode(),
            GCSettings.IsServerGC,
            GCSettings.LatencyMode.ToString(),
            command,
            invocationContext,
            DateTimeOffset.UtcNow,
            "VC-G0-FP-0.1.0",
            g0Status);
    }

    private static bool GetGitDirtyState()
    {
        string? status = RunProcess("git", ["status", "--porcelain", "--untracked-files=normal"]);
        return status is null || status.Length > 0;
    }

    private static string GetSdkVersion()
    {
        string executable = "dotnet";
        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            string candidate = Path.Combine(dotnetRoot, System.OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(candidate))
            {
                executable = candidate;
            }
        }

        return RunProcess(executable, ["--version"]) ?? "unknown";
    }

    private static SourceIdentity CaptureSourceIdentity(string? repositoryRoot)
    {
        const string method = "SHA-256 over ordinal repository-relative paths and bytes for Git tracked/untracked nonignored files; diff hash also includes git diff --binary --full-index HEAD.";
        if (repositoryRoot is null)
        {
            return new SourceIdentity("unknown", "unknown", method);
        }

        try
        {
            string? allFiles = RunProcess(
                "git",
                ["-C", repositoryRoot, "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                trim: false);
            string? untrackedFiles = RunProcess(
                "git",
                ["-C", repositoryRoot, "ls-files", "-z", "--others", "--exclude-standard"],
                trim: false);
            string? diff = RunProcess(
                "git",
                ["-C", repositoryRoot, "diff", "--binary", "--full-index", "--no-ext-diff", "HEAD", "--"],
                trim: false);
            if (allFiles is null || untrackedFiles is null || diff is null)
            {
                return new SourceIdentity("unknown", "unknown", method);
            }

            string[] paths = SplitNullDelimited(allFiles);
            string sourceHash = HashPaths(repositoryRoot, paths, prefix: null);
            string diffHash = HashPaths(
                repositoryRoot,
                SplitNullDelimited(untrackedFiles),
                Encoding.UTF8.GetBytes(diff));
            return new SourceIdentity(sourceHash, diffHash, method);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new SourceIdentity("unknown", "unknown", $"{method} Capture failed: {exception.GetType().Name}.");
        }
    }

    private static string[] SplitNullDelimited(string value)
    {
        string[] paths = value.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(paths, StringComparer.Ordinal);
        return paths;
    }

    private static string HashPaths(string repositoryRoot, string[] paths, byte[]? prefix)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (prefix is not null)
        {
            AppendHashField(hash, prefix);
        }

        foreach (string relativePath in paths)
        {
            AppendHashField(hash, Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/')));
            string fullPath = Path.Combine(repositoryRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                AppendHashInt64(hash, -1);
                continue;
            }

            using FileStream stream = File.OpenRead(fullPath);
            AppendHashInt64(hash, stream.Length);
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendHashField(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        AppendHashInt64(hash, value.Length);
        hash.AppendData(value);
    }

    private static void AppendHashInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static string ComputeFileSha256(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "unknown";
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return $"unknown ({exception.GetType().Name})";
        }
    }

    private static HostMemory CaptureHostMemory()
    {
        if (System.OperatingSystem.IsLinux())
        {
            const string memInfoPath = "/proc/meminfo";
            if (File.Exists(memInfoPath))
            {
                long? total = null;
                long? available = null;
                foreach (string line in File.ReadLines(memInfoPath))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        total = ParseMemInfoBytes(line);
                    }
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        available = ParseMemInfoBytes(line);
                    }
                }

                return new HostMemory(total, available, "/proc/meminfo");
            }
        }

        if (System.OperatingSystem.IsMacOS())
        {
            string? totalText = RunProcess("sysctl", ["-n", "hw.memsize"]);
            long? total = long.TryParse(totalText, NumberStyles.None, CultureInfo.InvariantCulture, out long totalBytes)
                ? totalBytes
                : null;
            return new HostMemory(total, null, "sysctl hw.memsize; available physical memory unknown");
        }

        return new HostMemory(null, null, "unknown; managed process memory budget is reported separately");
    }

    private static long? ParseMemInfoBytes(string line)
    {
        string[] components = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return components.Length >= 2
            && long.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out long kibibytes)
            ? checked(kibibytes * 1024L)
            : null;
    }

    private static string GetProcessAffinity()
    {
        if (System.OperatingSystem.IsLinux())
        {
            const string statusPath = "/proc/self/status";
            if (File.Exists(statusPath))
            {
                foreach (string line in File.ReadLines(statusPath))
                {
                    if (line.StartsWith("Cpus_allowed_list:", StringComparison.Ordinal))
                    {
                        return line[(line.IndexOf(':') + 1)..].Trim();
                    }
                }
            }
        }

        if (System.OperatingSystem.IsWindows())
        {
            try
            {
                using Process process = Process.GetCurrentProcess();
                return $"0x{unchecked((ulong)process.ProcessorAffinity.ToInt64()):X}";
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                return $"unknown ({exception.GetType().Name})";
            }
        }

        return "unknown";
    }

    private static string GetMachineModel()
    {
        if (System.OperatingSystem.IsLinux())
        {
            string? productName = ReadTrimmedFile("/sys/devices/virtual/dmi/id/product_name");
            string? productVersion = ReadTrimmedFile("/sys/devices/virtual/dmi/id/product_version");
            if (productName is not null)
            {
                return productVersion is null ? productName : $"{productName} {productVersion}";
            }
        }

        return System.OperatingSystem.IsMacOS()
            ? RunProcess("sysctl", ["-n", "hw.model"]) ?? "unknown"
            : "unknown";
    }

    private static string GetPowerMode()
    {
        if (System.OperatingSystem.IsLinux())
        {
            List<string> values = [];
            AddFileValue(values, "platform-profile", "/sys/firmware/acpi/platform_profile");
            AddFileValue(values, "cpu-governor", "/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor");
            return values.Count == 0 ? "unknown" : string.Join("; ", values);
        }

        return System.OperatingSystem.IsWindows()
            ? RunProcess("powercfg", ["/getactivescheme"]) ?? "unknown"
            : System.OperatingSystem.IsMacOS()
                ? RunProcess("pmset", ["-g", "custom"]) ?? "unknown"
                : "unknown";
    }

    private static void AddFileValue(List<string> values, string label, string path)
    {
        string? value = ReadTrimmedFile(path);
        if (value is not null)
        {
            values.Add($"{label}={value}");
        }
    }

    private static string? ReadTrimmedFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string GetCpuDescription()
    {
        string? environmentCpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        if (!string.IsNullOrWhiteSpace(environmentCpu))
        {
            return environmentCpu;
        }

        const string cpuInfoPath = "/proc/cpuinfo";
        if (File.Exists(cpuInfoPath))
        {
            foreach (string line in File.ReadLines(cpuInfoPath))
            {
                if (!line.StartsWith("model name", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator >= 0)
                {
                    return line[(separator + 1)..].Trim();
                }
            }
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{RuntimeInformation.ProcessArchitecture}; {Environment.ProcessorCount} logical processors visible");
    }

    private static string? RunProcess(
        string executable,
        IReadOnlyList<string> arguments,
        bool trim = true)
    {
        try
        {
            ProcessStartInfo startInfo = new(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            ProcessExecutionResult result = SectionBenchmarkSupport.RunProcess(startInfo, ProbeTimeout);
            return result.TimedOut || result.ExitCode != 0
                ? null
                : trim ? result.StandardOutput.Trim() : result.StandardOutput;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return null;
        }
    }

    private sealed record SourceIdentity(
        string SourceTreeSha256,
        string WorkingTreeDiffSha256,
        string Method);

    private readonly record struct HostMemory(
        long? TotalBytes,
        long? AvailableBytes,
        string Discovery);
}

internal sealed record ProcessExecutionResult(
    int? ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError);
