using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Uno.Data;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;
using UI = Uno.Controls.Models.TreeDataGrid;

if (args.Length != 2) { Console.Error.WriteLine("Usage: <revision> <output.json>"); return 2; }
try
{
    var root = new Node { Number = 17, Child = new() { Number = 17 } };
    Func<Node, int> read = static x => x.Child!.Number;
    Func<Node, int> alternateRead = static x => x.Child!.Number;
    Action<Node, int> write = static (x, value) => x.Child!.Number = value;
    Func<Node, object> rootLink = static x => x;
    Func<Node, object> alternateRootLink = static x => x;
    Func<Node, object>[] links = [rootLink, static x => x.Child!];
    var descriptor = new TypedBinding<Node, int> { Read = read, Write = write, Links = links, Mode = BindingMode.TwoWay };
    var source = new Root(root);
    var recorder = new Recorder();
    var column = new Column(read, descriptor);
    var workloads = new (string Name, bool Observed, bool Transient, bool Direct, bool Cell, BindingMode Mode, int Mutation)[]
    {
        ("descriptor-create", false, false, false, false, BindingMode.OneWay, 0),
        ("descriptor-observe", true, false, false, false, BindingMode.OneWay, 0),
        ("descriptor-one-time", true, false, false, false, BindingMode.OneTime, 0),
        ("custom-cell-create", true, false, false, true, BindingMode.OneWay, 0),
        ("direct-constructor", false, false, true, false, BindingMode.OneWay, 0),
        ("transient-descriptor", false, true, false, false, BindingMode.OneWay, 0),
        ("changed-reader", false, false, false, false, BindingMode.OneWay, 1),
        ("changed-link", false, false, false, false, BindingMode.OneWay, 2),
        ("changed-fallback", false, false, false, false, BindingMode.OneWay, 3),
    };
    const int count = 4096;
    const int warmupBatches = 8;
    const int samplesPerWorkload = 15;
    var samples = new List<Sample>();
    foreach (var workload in workloads)
    {
        // Reset descriptor state before each workload, not between measured batches.
        descriptor.Read = read; descriptor.Links = (Func<Node, object>[])links.Clone();
        for (var i = 0; i < warmupBatches; ++i) Verify(Run());
        for (var i = 0; i < samplesPerWorkload; ++i)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var checksum = Run();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Verify(checksum);
            samples.Add(new(workload.Name, i, count, elapsed, allocated, checksum));
        }
        long Run()
        {
            long checksum = 0;
            for (var j = 0; j < count; ++j)
            {
                if (workload.Mutation == 1) descriptor.Read = (j & 1) == 0 ? read : alternateRead;
                if (workload.Mutation == 2) descriptor.Links![0] = (j & 1) == 0 ? rootLink : alternateRootLink;
                if (workload.Mutation == 3) descriptor.FallbackValue = j;
                var active = workload.Transient ? new TypedBinding<Node, int>
                { Read = read, Write = write, Links = links, Mode = BindingMode.TwoWay } : descriptor;
                using var expression = workload.Direct ? new TypedBindingExpression<Node, int>(source, read, write, links, default)
                    : workload.Cell ? column.Bind(root) : active.Instance(root, workload.Mode);
                if (workload.Cell)
                {
                    using var cell = new UI.TextCell<int>(expression, false);
                    checksum += cell.Value;
                }
                else if (workload.Observed)
                {
                    using var subscription = expression.Subscribe(recorder);
                    checksum += recorder.Last;
                }
                else checksum += 17;
            }
            return checksum;
        }
        void Verify(long checksum)
        {
            if (checksum != count * 17 || root.Subscribers != 0 || root.Child!.Subscribers != 0)
                throw new InvalidOperationException($"{workload.Name}: checksum or subscription cleanup failed.");
        }
    }
    var library = typeof(TypedBinding<Node, int>).Assembly.Location;
    var report = new
    {
        schemaVersion = 1, revision = args[0],
        librarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(library))),
        runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        os = RuntimeInformation.OSDescription, serverGc = GCSettings.IsServerGC,
        dynamicCodeCompiled = RuntimeFeature.IsDynamicCodeCompiled,
        samplesPerWorkload, warmupBatches,
        tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),
        scope = "Managed typed-expression creation/subscription/disposal and native TextCell model creation. Direct construction, transient descriptors and mutation costs retained separately; not native controls/layout/frame rate.",
        samples,
    };
    File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_BINDING_PLAN_EXECUTION_PASSED=" + args[1]);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

internal sealed record Sample(string Operation, int Iteration, int Count, double Milliseconds, long AllocatedBytes, long Checksum);
internal sealed class Recorder : IObserver<BindingValue<int>>
{
    public int Last;
    public void OnNext(BindingValue<int> value)
    { if (!value.HasValue || value.HasError) throw new InvalidOperationException("Invalid bound value."); Last = value.Value; }
    public void OnError(Exception error) => throw error;
    public void OnCompleted() { }
}
internal sealed class Root(Node root) : IObservable<Node?>, IDisposable
{
    public IDisposable Subscribe(IObserver<Node?> observer) { observer.OnNext(root); return this; }
    public void Dispose() { }
}
internal sealed class Column(Func<Node, int> read, TypedBinding<Node, int> descriptor)
    : UI.ColumnBase<Node, int>("Number", read, descriptor, null, null)
{
    public TypedBindingExpression<Node, int> Bind(Node model) => CreateBindingExpression(model);
    public override UI.ICell CreateCell(TreeDataGridCore.Models.IRow<Node> row) => new UI.TextCell<int>(Bind(row.Model), false);
}
internal sealed class Node : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    public int Number { get; set; }
    public Node? Child { get; set; }
    public int Subscribers { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { _changed += value; if (value is not null) ++Subscribers; }
        remove { _changed -= value; if (value is not null) --Subscribers; }
    }
}
