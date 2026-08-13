using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace VibeCraft.G1.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
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
