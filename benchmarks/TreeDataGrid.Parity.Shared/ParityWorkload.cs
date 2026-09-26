using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TreeDataGridCore;
using TreeDataGridCore.Models;

namespace TreeDataGrid.Parity;

public sealed record BenchRow(int Id, string Label);
public sealed record Frame(double X, double Y, double Width, double Height, int Rows, int Cells, string? Error);
public sealed record Measurement(string Operation, int Iteration, double SynchronousUiMilliseconds,
    long SynchronousUiAllocatedBytes, double SettledMilliseconds, Frame Frame);

public interface INativeParityHost : IDisposable
{
    string Framework { get; }
    string UiAssembly { get; }
    void Bind(FlatTreeDataGridSource<BenchRow> source);
    void Scroll(double x, double y);
    void UpdateLayout();
    Frame Inspect(FlatTreeDataGridSource<BenchRow> source, double x, double y);
}

/// <summary>One identical workload and measurement protocol for both native UI hosts.</summary>
public static class ParityWorkload
{
    public const int RowCount = 10_000;
    public const int Width = 800;
    public const int Height = 480;
    public const int RowHeight = 32;
    public const int ColumnWidth = 128;
    private static readonly string[] Operations = ["replace-visible-row", "resize-visible-column", "sort", "scroll-y", "scroll-x", "distant-diagonal-scroll"];

    public static async Task RunAsync(INativeParityHost host)
    {
        var columns = ReadCount("PARITY_COLUMNS", 64, 8, 1000);
        var iterations = ReadCount("PARITY_ITERATIONS", 25, 5, 500);
        const int warmup = 5;
        var measurements = new List<Measurement>(Operations.Length * iterations);
        foreach (var operation in Operations)
        {
            var items = new ObservableCollection<BenchRow>(Enumerable.Range(0, RowCount)
                .Select(index => new BenchRow(index, $"R{index:D5}")));
            using var source = new FlatTreeDataGridSource<BenchRow>(items);
            for (var column = 0; column < columns; ++column)
                source.Columns.Add(new TextColumn<BenchRow, string>($"C{column:D4}", row => row.Label, width: new(ColumnWidth)));
            host.Bind(source);
            host.Scroll(0, 0);
            // Startup, source construction, first templates and JIT are excluded.
            await Task.Delay(200);
            await SettleAsync(host, source, 0, 0);
            for (var iteration = -warmup; iteration < iterations; ++iteration)
            {
                // Permit painting and pending native callbacks between operations.
                // This pacing is outside all measured intervals.
                await Task.Delay(20);
                var index = iteration + warmup;
                var x = 0d;
                var y = 0d;
                var thread = Environment.CurrentManagedThreadId;
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                switch (operation)
                {
                    case "replace-visible-row":
                        items[7] = new(7, $"R00007/{index:D4}");
                        break;
                    case "resize-visible-column":
                        source.Columns[3].Width = new(ColumnWidth + (index % 2) * 16);
                        break;
                    case "sort":
                        if (!source.SortBy(source.Columns[0], index % 2 == 0 ? ListSortDirection.Descending : ListSortDirection.Ascending))
                            throw new InvalidOperationException("The shared Core source rejected sorting.");
                        break;
                    case "scroll-y": y = RowHeight * (index + 1) * 3; host.Scroll(x, y); break;
                    case "scroll-x": x = ColumnWidth * ((index + 1) % (columns - 7)); host.Scroll(x, y); break;
                    case "distant-diagonal-scroll":
                        x = ColumnWidth * (index % (columns - 7));
                        y = RowHeight * ((index * 997 + 521) % 9000);
                        host.Scroll(x, y);
                        break;
                }
                host.UpdateLayout();
                var synchronousEnd = Stopwatch.GetTimestamp();
                var allocatedDelta = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (thread != Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("Synchronous UI allocation measurement crossed threads.");
                var frame = await SettleAsync(host, source, x, y);
                var settledEnd = Stopwatch.GetTimestamp();
                if (iteration >= 0)
                    measurements.Add(new(operation, iteration,
                        Stopwatch.GetElapsedTime(started, synchronousEnd).TotalMilliseconds,
                        allocatedDelta, Stopwatch.GetElapsedTime(started, settledEnd).TotalMilliseconds, frame));
            }
        }
        var report = new
        {
            schemaVersion = 1, framework = host.Framework, uiAssembly = host.UiAssembly,
            revision = Environment.GetEnvironmentVariable("PARITY_REVISION"),
            runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            os = RuntimeInformation.OSDescription, serverGc = System.Runtime.GCSettings.IsServerGC,
            workload = new { id = "native-fixed-text-v1", rows = RowCount, columns, width = Width, height = Height,
                rowHeight = RowHeight, columnWidth = ColumnWidth, font = "DejaVu Sans", fontSize = 14,
                headers = false, scrollbars = "Hidden", cacheLength = 0, warmup, iterations },
            scope = "Synchronous UI-thread source/layout work and verified layout-settlement latency. Not GPU completion, frame rate, input latency, accessibility, variable-height or all-feature parity.",
            completePerformanceParityProven = false, measurements,
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var output = Environment.GetEnvironmentVariable("PARITY_OUTPUT")
            ?? throw new InvalidOperationException("PARITY_OUTPUT must identify the output JSON file.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, json);
        Console.WriteLine($"TREEGRID_PARITY_PASSED: {host.Framework}; measurements={measurements.Count}; output={output}");
    }

    private static async Task<Frame> SettleAsync(INativeParityHost host, FlatTreeDataGridSource<BenchRow> source, double x, double y)
    {
        var started = Stopwatch.GetTimestamp();
        Frame frame;
        do
        {
            host.UpdateLayout();
            frame = host.Inspect(source, x, y);
            if (frame.Error is null) return frame;
            await Task.Delay(1);
        } while (Stopwatch.GetElapsedTime(started).TotalSeconds < 3);
        throw new InvalidOperationException($"Native layout failed correctness/geometry gate: {frame}");
    }
    private static int ReadCount(string name, int fallback, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (value is null) return fallback;
        return int.TryParse(value, out var count) && count >= minimum && count <= maximum
            ? count : throw new ArgumentOutOfRangeException(name, value, $"Expected {minimum}..{maximum}.");
    }
}
