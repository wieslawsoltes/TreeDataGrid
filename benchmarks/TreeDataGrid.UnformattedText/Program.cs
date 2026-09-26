using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;

if (args.Length != 2) { Console.Error.WriteLine("Usage: <revision> <output.json>"); return 2; }
var owned = new List<IDisposable>();
try
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    const int count = 8192, warmups = 12, sampleCount = 25;
    var workloads = new List<(string Name, Func<string?> Query, string? Expected)>();
    Add<int>("live-int-raw", 123456789, true, false);
    Add<int>("core-int-raw", 123456789, false, false);
    Add<decimal>("live-decimal-raw", 12345.6789m, true, false);
    Add<int?>("live-nullable-raw", 123456789, true, false);
    Add<int?>("core-nullable-null", null, false, false);
    Add<string>("live-string-raw", "same string", true, false);
    Add<object>("core-object-raw", 123456789, false, false);
    Add<int>("live-int-formatted", 123456789, true, true);
    Add<int>("core-int-formatted", 123456789, false, true);
    var scalar = new U.TextCell<int>(123456789);
    owned.Add(scalar);
    workloads.Add(("scalar-int-control", () => scalar.Text, "123456789"));
    var samples = new List<Sample>();
    foreach (var workload in workloads)
    {
        for (var batch = 0; batch < warmups; ++batch) Verify(Run(), workload);
        for (var iteration = 0; iteration < sampleCount; ++iteration)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var checksum = Run();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Verify(checksum, workload);
            samples.Add(new(workload.Name, iteration, count, elapsed, allocated, checksum));
        }
        long Run()
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i) checksum += workload.Query()?.Length ?? -1;
            return checksum;
        }
    }
    var report = new
    {
        schemaVersion = 1, revision = args[0],
        librarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CellColumn).Assembly.Location))),
        runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(), serverGc = GCSettings.IsServerGC,
        tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"), count, warmups, sampleCount,
        scope = "Warm public bound-cell Text queries. Raw numeric paths plus nullable, string, object, formatted and scalar controls. Construction, native control layout, GPU completion and frame rate are excluded.",
        samples,
    };
    File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_UNFORMATTED_TEXT_EXECUTION_PASSED=" + args[1]);
    return 0;

    void Add<T>(string name, T? value, bool live, bool formatted)
    {
        var model = new Model<T> { Value = value };
        var source = new FlatTreeDataGridSource<Model<T>>([model]);
        owned.Add(source);
        var definition = new ValueColumn<Model<T>, T?>("Value", x => x.Value);
        source.Columns.Add(definition);
        CellColumn column = live
            ? new U.TextColumn<Model<T>, T>(definition, new() { StringFormat = formatted ? "[{0}]" : null!, Culture = CultureInfo.InvariantCulture })
            : new ValueCellColumn<Model<T>, T?>(definition, CellKind.Text,
                formatted ? new TextCellOptions { StringFormat = "[{0}]", Culture = CultureInfo.InvariantCulture } : null);
        owned.Add(column);
        var cell = column.CreateCell(source.Rows[0]);
        owned.Add(cell);
        var text = (U.ITextCell)cell;
        string? expected = formatted ? string.Format(CultureInfo.InvariantCulture, "[{0}]", value) : value?.ToString();
        if (text.Text != expected) throw new InvalidOperationException("Incorrect initial text: " + name);
        workloads.Add((name, () => text.Text, expected));
    }
    void Verify(long checksum, (string Name, Func<string?> Query, string? Expected) workload)
    {
        if (checksum != (long)count * (workload.Expected?.Length ?? -1) || workload.Query() != workload.Expected)
            throw new InvalidOperationException("Incorrect measured work: " + workload.Name);
    }
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
finally
{
    for (var i = owned.Count - 1; i >= 0; --i) owned[i].Dispose();
}
internal sealed class Model<T> { public T? Value { get; set; } }
internal sealed record Sample(string Operation, int Iteration, int Count, double Milliseconds, long AllocatedBytes, long Checksum);
