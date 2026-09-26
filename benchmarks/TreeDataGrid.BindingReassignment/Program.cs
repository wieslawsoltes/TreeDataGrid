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
    Func<Node, int> read = static node => node.Child!.Number;
    Func<Node, int> alternateRead = static node => node.Child!.Number;
    Action<Node, int> write = static (node, value) => node.Child!.Number = value;
    Func<Node, object> rootLink = static node => node;
    Func<Node, object> alternateRootLink = static node => node;
    Func<Node, object> childLink = static node => node.Child!;
    var source = new Root(root);
    var recorder = new Recorder();
    var samples = new List<Sample>();
    const int count = 4096, warmupBatches = 8, samplesPerWorkload = 15;
    var workloads = new (string Name, Assignment Assignment, bool Observe, bool Cell, bool Transient, bool Direct)[]
    {
        ("stable", Assignment.None, false, false, false, false),
        ("same-read", Assignment.Read, false, false, false, false),
        ("same-write", Assignment.Write, false, false, false, false),
        ("same-links", Assignment.Links, false, false, false, false),
        ("same-all", Assignment.All, false, false, false, false),
        ("same-all-observed", Assignment.All, true, false, false, false),
        ("same-all-text-cell", Assignment.All, false, true, false, false),
        ("changed-read", Assignment.ChangedRead, false, false, false, false),
        ("replaced-links", Assignment.ReplacedLinks, false, false, false, false),
        ("mutated-links", Assignment.MutatedLinks, false, false, false, false),
        ("transient", Assignment.None, false, false, true, false),
        ("direct", Assignment.None, false, false, false, true),
    };
    foreach (var workload in workloads)
    {
        var links = new[] { rootLink, childLink };
        var descriptor = NewDescriptor();
        for (var i = 0; i < warmupBatches; ++i) Verify(Run());
        for (var iteration = 0; iteration < samplesPerWorkload; ++iteration)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            var checksum = Run();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Verify(checksum);
            samples.Add(new(workload.Name, iteration, count, elapsed, allocated, checksum));
        }
        TypedBinding<Node, int> NewDescriptor() => new() { Read = read, Write = write, Links = links };
        long Run()
        {
            long checksum = 0;
            for (var j = 0; j < count; ++j)
            {
                switch (workload.Assignment)
                {
                    case Assignment.Read: descriptor.Read = read; break;
                    case Assignment.Write: descriptor.Write = write; break;
                    case Assignment.Links: descriptor.Links = links; break;
                    case Assignment.All: descriptor.Read = read; descriptor.Write = write; descriptor.Links = links; break;
                    case Assignment.ChangedRead: descriptor.Read = (j & 1) == 0 ? read : alternateRead; break;
                    case Assignment.ReplacedLinks: descriptor.Links = (Func<Node, object>[])links.Clone(); break;
                    case Assignment.MutatedLinks: links[0] = (j & 1) == 0 ? rootLink : alternateRootLink; descriptor.Links = links; break;
                }
                var active = workload.Transient ? NewDescriptor() : descriptor;
                using var expression = workload.Direct
                    ? new TypedBindingExpression<Node, int>(source, read, write, links, default)
                    : active.Instance(root);
                if (workload.Cell)
                {
                    using var cell = new UI.TextCell<int>(expression, false);
                    checksum += cell.Value;
                }
                else if (workload.Observe)
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
                throw new InvalidOperationException(workload.Name + ": checksum or subscription cleanup mismatch.");
        }
    }
    var report = new
    {
        schemaVersion = 1, revision = args[0],
        librarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(TypedBinding<Node, int>).Assembly.Location))),
        runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        os = RuntimeInformation.OSDescription, serverGc = GCSettings.IsServerGC,
        dynamicCodeCompiled = RuntimeFeature.IsDynamicCodeCompiled,
        tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),
        count, warmupBatches, samplesPerWorkload,
        scope = "Managed descriptor reassignment and expression creation/disposal; separate observed and TextCell-model paths. No native-control layout, whole-grid sorting, startup or frame-rate claim.",
        samples,
    };
    File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_BINDING_REASSIGNMENT_EXECUTION_PASSED=" + args[1]);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

internal enum Assignment { None, Read, Write, Links, All, ChangedRead, ReplacedLinks, MutatedLinks }
internal sealed record Sample(string Operation, int Iteration, int Count, double Milliseconds, long AllocatedBytes, long Checksum);
internal sealed class Recorder : IObserver<BindingValue<int>>
{
    internal int Last;
    public void OnNext(BindingValue<int> value)
    {
        if (!value.HasValue || value.HasError) throw new InvalidOperationException("Invalid observed value.");
        Last = value.Value;
    }
    public void OnError(Exception error) => throw error;
    public void OnCompleted() { }
}
internal sealed class Root(Node root) : IObservable<Node?>, IDisposable
{
    public IDisposable Subscribe(IObserver<Node?> observer) { observer.OnNext(root); return this; }
    public void Dispose() { }
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
