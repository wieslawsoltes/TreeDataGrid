using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Uno.Data.Core.Parsers;
using Uno.Experimental.Data.Core;
using U = Uno.Controls.Models.TreeDataGrid;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: TreeDataGrid.AccessorExecution <revision> <output.json>");
    return 2;
}

try
{
    const int samplesPerWorkload = 15;
    var samples = new List<Sample>();
    var root = new Node { Child = new() { Child = new() { Number = 17 } } };
    var second = new Node { Child = new() { Child = new() { Number = 29 } } };
    Expression<Func<Node, int>> selector = x => x.Child!.Child!.Number;
    var links = ExpressionChainVisitor<Node>.Build(selector);
    if (links.Length != 3 || !ReferenceEquals(links[2](root), root.Child!.Child))
        throw new InvalidOperationException("Unexpected owner-link chain.");
    var column = new ProbeColumn(selector);
    var comparison = column.GetComparison(ListSortDirection.Ascending)!;
    using var expression = column.Bind(root);
    var observer = new Recorder();
    using var subscription = expression.Subscribe(observer);
    var leaf = root.Child!.Child!;
    var workloads = new (string Name, int Count, Func<int, long> Run)[]
    {
        ("owner-link-chain", 32768, count =>
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i)
                foreach (var link in links)
                    if (link(root) is Node) ++checksum;
            return checksum;
        }),
        ("custom-column-select", 65536, count =>
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i) checksum += column.ValueSelector(root);
            return checksum;
        }),
        ("custom-column-compare", 32768, count =>
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i) checksum += Math.Sign(comparison(root, second));
            return checksum;
        }),
        ("typed-binding-notification", 4096, count =>
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i)
            {
                leaf.Number = (i & 1) == 0 ? 18 : 17;
                checksum += observer.Last;
            }
            return checksum;
        }),
        ("column-construction", 16, count =>
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i)
            {
                var created = new ProbeColumn(selector);
                checksum += created.ValueSelector(root);
            }
            return checksum;
        }),
    };
    var expectedPerOperation = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["owner-link-chain"] = 3,
        ["custom-column-select"] = 17,
        ["custom-column-compare"] = -1,
        ["typed-binding-notification"] = 17.5,
        ["column-construction"] = 17,
    };
    // Construction is deliberately measured separately: JIT compilation must
    // not be hidden behind invocation throughput numbers.
    foreach (var workload in workloads)
    {
        for (var i = 0; i < 8; ++i)
            Verify(workload.Name, workload.Count, workload.Run(workload.Count));
        for (var i = 0; i < samplesPerWorkload; ++i)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var checksum = workload.Run(workload.Count);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Verify(workload.Name, workload.Count, checksum);
            samples.Add(new(workload.Name, i, workload.Count, elapsed, allocated, checksum));
        }
    }
    if (observer.Last != 17 || root.Subscribers != 1 || root.Child!.Subscribers != 1 || leaf.Subscribers != 1)
        throw new InvalidOperationException("Typed-binding value or subscription mismatch.");
    subscription.Dispose();
    if (root.Subscribers != 0 || root.Child!.Subscribers != 0 || leaf.Subscribers != 0)
        throw new InvalidOperationException("Typed-binding subscription cleanup failed.");
    var library = typeof(ProbeColumn).BaseType!.Assembly.Location;
    var report = new
    {
        schemaVersion = 1,
        revision = args[0],
        librarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(library))),
        runtime = RuntimeInformation.FrameworkDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        os = RuntimeInformation.OSDescription,
        serverGc = GCSettings.IsServerGC,
        dynamicCodeCompiled = RuntimeFeature.IsDynamicCodeCompiled,
        samplesPerWorkload,
        warmupBatches = 8,
        scope = "Managed public owner links, custom-column selectors/comparisons, typed binding notifications and separate column construction. Not native controls, complete sorting, rendering or frame time.",
        samples,
    };
    var destination = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.WriteAllText(destination, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_ACCESSOR_EXECUTION_PASSED=" + destination);
    return 0;

    void Verify(string name, int count, long checksum)
    {
        if (checksum != expectedPerOperation[name] * count)
            throw new InvalidOperationException($"Invalid {name} checksum: {checksum} for {count} operations.");
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}

internal sealed record Sample(string Operation, int Iteration, int Count, double Milliseconds, long AllocatedBytes, long Checksum);

internal sealed class ProbeColumn : U.ColumnBase<Node, int>
{
    public ProbeColumn(Expression<Func<Node, int>> getter) : base("Number", getter, null, null, new()) { }
    public TypedBindingExpression<Node, int> Bind(Node root) => CreateBindingExpression(root);
    public override U.ICell CreateCell(TreeDataGridCore.Models.IRow<Node> row) => new U.TextCell<int>(ValueSelector(row.Model));
}

internal sealed class Recorder : IObserver<Uno.Data.BindingValue<int>>
{
    public int Last;
    public void OnNext(Uno.Data.BindingValue<int> value)
    {
        if (value.HasValue) Last = value.Value;
    }
    public void OnError(Exception error) => throw new InvalidOperationException("Binding failed.", error);
    public void OnCompleted() { }
}

internal sealed class Node : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs NumberChanged = new(nameof(Number));
    private int _number;
    private PropertyChangedEventHandler? _changed;
    public Node? Child { get; set; }
    public int Subscribers { get; private set; }
    public int Number
    {
        get => _number;
        set
        {
            if (_number == value) return;
            _number = value;
            _changed?.Invoke(this, NumberChanged);
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { _changed += value; if (value is not null) ++Subscribers; }
        remove { _changed -= value; if (value is not null) --Subscribers; }
    }
}
