using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using VibeCraft.G1.Benchmarks.Sections;

namespace VibeCraft.G1.Benchmarks;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--e1-report", StringComparison.Ordinal))
        {
            return E1CoreDataReport.Run(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--e1-memory-child", StringComparison.Ordinal))
        {
            return E1CoreDataReport.RunMemoryChild(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--section-memory-report", StringComparison.Ordinal))
        {
            return SectionRetainedMemoryReport.RunParent(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--section-memory-trial", StringComparison.Ordinal))
        {
            return SectionRetainedMemoryReport.RunChild(args[1..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--section-amplification-report", StringComparison.Ordinal))
        {
            return SectionAmplificationReport.Run(args[1..]);
        }

        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}

[MemoryDiagnoser]
public class BootstrapBenchmark
{
    [Params(1024, 4096)]
    public int Count { get; set; }

    [Benchmark]
    public int IntegerLoop()
    {
        int sum = 0;
        for (int index = 0; index < Count; index++)
        {
            sum = unchecked((sum * 31) + index);
        }

        return sum;
    }
}
