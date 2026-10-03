using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

// Managed model/factory creation diagnostic. This deliberately does not construct
// native controls or claim frame-time, input, rendering or whole-grid performance.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: TreeDataGrid.BindingCreation <source-revision> <output.json>");
    return 2;
}
try
{
    const int warmupBatches = 64;
    const int warmupSize = 1024;
    const int batchSize = 8192;
    const int samplesPerModel = 15;
    var samples = new List<Sample>(samplesPerModel * 4);
    var definition = ValueColumn<Probe, string>.FromDelegate("Name", static model => model.Name);
    using var column = new ValueCellColumn<Probe, string>(definition, CellKind.Text);
    foreach (var kind in new[] { "plain", "property", "collection", "both" })
    {
        Probe model = kind switch
        {
            "property" => new PropertyProbe(),
            "collection" => new CollectionProbe(),
            "both" => new BothProbe(),
            _ => new Probe(),
        };
        var row = new Row(model);
        for (var i = 0; i < warmupBatches; ++i) RunBatch(column, row, warmupSize);
        for (var i = 0; i < samplesPerModel; ++i)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var checksum = RunBatch(column, row, batchSize);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            if (checksum != batchSize * model.Name.Length || model.PropertySubscribers != 0 || model.CollectionSubscribers != 0)
                throw new InvalidOperationException("Factory value or complete observation cleanup validation failed.");
            samples.Add(new(kind, i, batchSize, elapsed, bytes, checksum));
        }
        GC.KeepAlive(model);
    }
    var assembly = typeof(CellValue).Assembly.Location;
    var report = new
    {
        schemaVersion = 1,
        revision = args[0],
        librarySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly))),
        runtime = RuntimeInformation.FrameworkDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        os = RuntimeInformation.OSDescription,
        serverGc = System.Runtime.GCSettings.IsServerGC,
        warmupBatches,
        warmupSize,
        batchSize,
        samplesPerModel,
        scope = "Managed value-column CreateCell/value read/Dispose with borrowed row and column. Complete notification cleanup verified. Not a native-control, frame-time, rendering or whole-grid benchmark.",
        samples,
    };
    File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_BINDING_CREATION_DIAGNOSTIC_PASSED: " + args[1]);
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}

static int RunBatch(ValueCellColumn<Probe, string> column, Row row, int count)
{
    var checksum = 0;
    for (var i = 0; i < count; ++i)
    {
        using var cell = column.CreateCell(row);
        if (cell.Value is not string value || !ReferenceEquals(value, row.Model.Name))
            throw new InvalidOperationException("Factory did not return the current row value.");
        checksum += value.Length;
    }
    return checksum;
}

internal sealed record Sample(string Kind, int Iteration, int Count, double Milliseconds, long AllocatedBytes, int Checksum);

internal sealed class Row(Probe model) : IRow<Probe>
{
    public Probe Model => model;
    object? IRow.Model => Model;
    public object? Header => null;
    public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    public void UpdateModelIndex(int delta) { }
}

internal class Probe
{
    internal string Name { get; } = "Same model value";
    internal int PropertySubscribers;
    internal int CollectionSubscribers;
}

internal class PropertyProbe : Probe, INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _handlers;
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { _handlers += value; ++PropertySubscribers; }
        remove { _handlers -= value; --PropertySubscribers; }
    }
}

internal sealed class CollectionProbe : Probe, INotifyCollectionChanged
{
    private NotifyCollectionChangedEventHandler? _handlers;
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { _handlers += value; ++CollectionSubscribers; }
        remove { _handlers -= value; --CollectionSubscribers; }
    }
}

internal sealed class BothProbe : PropertyProbe, INotifyCollectionChanged
{
    private NotifyCollectionChangedEventHandler? _handlers;
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { _handlers += value; ++CollectionSubscribers; }
        remove { _handlers -= value; --CollectionSubscribers; }
    }
}
