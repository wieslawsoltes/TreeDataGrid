using BenchmarkDotNet.Running;

namespace TreeDataGridBenchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--compare-scroll-revisions")
        {
            RevisionScrollComparison.Run(args[1..]);
            return;
        }
        if (args.Length == 1 && args[0] == "--paired-vertical-scroll")
        {
            PairedScrollComparison.Run();
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
