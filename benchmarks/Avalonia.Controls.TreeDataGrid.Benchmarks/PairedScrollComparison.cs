using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace TreeDataGridBenchmarks;

// Alternate APIs within one warmed process to control drift between isolated BDN processes.
// Keep exactly the existing VerticalRowScrolls workload and exclude setup/cleanup from timing.
internal static class PairedScrollComparison
{
    public static void Run()
    {
        var benchmark = new VirtualizationBenchmarks();
        var results = new List<object>();
        benchmark.GlobalSetup();
        try
        {
            for (var round = -5; round < 10; ++round)
            for (var order = 0; order < 2; ++order)
            {
                benchmark.NeutralSource = ((round + order) & 1) == 0;
                benchmark.SetupVerticalRowScrolls();
                try
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    var allocation = GC.GetAllocatedBytesForCurrentThread();
                    var timer = Stopwatch.StartNew();
                    var count = benchmark.VerticalRowScrolls();
                    timer.Stop();
                    allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
                    if (round >= 0)
                        results.Add(new { Round = round, Native = benchmark.NeutralSource,
                            Microseconds = timer.Elapsed.TotalMicroseconds / 10_000,
                            AllocatedBytes = allocation / 10_000.0, RealizedRows = count });
                }
                finally { benchmark.CleanupVerticalRowScrolls(); }
            }
        }
        finally { benchmark.GlobalCleanup(); }
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
