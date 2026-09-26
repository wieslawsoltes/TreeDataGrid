using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Uno.Data;
using UI = Uno.Controls.Models.TreeDataGrid;

if (args.Length != 2) { Console.Error.WriteLine("Usage: <revision> <output.json>"); return 2; }
try
{
    var rawInt = new Source<int>(17);
    var rawBool = new Source<bool?>(true);
    var typedInt = new Source<BindingValue<int>>(17);
    var typedBool = new Source<BindingValue<bool?>>((bool?)true);
    ISource[] sources = [rawInt, rawBool, typedInt, typedBool];
    var workloads = new Workload[]
    {
        new("raw-text-readonly", 17, 0, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.TextCell<int>(rawInt, true); sum += cell.Value; } return sum; }),
        new("raw-text-editable", 19, 1, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.TextCell<int>(rawInt, false); cell.Value = 19; sum += cell.Value; } return sum; }),
        new("raw-checkbox-readonly", 1, 0, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.CheckBoxCell(rawBool, true, true); sum += cell.Value == true ? 1 : 0; } return sum; }),
        new("raw-checkbox-editable", 1, 1, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.CheckBoxCell(rawBool, false, true); cell.Value = null; sum += cell.Value is null ? 1 : 0; } return sum; }),
        new("typed-text-readonly", 17, 0, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.TextCell<int>(typedInt, true); sum += cell.Value; } return sum; }),
        new("typed-text-editable", 19, 1, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.TextCell<int>(typedInt, false); cell.Value = 19; sum += cell.Value; } return sum; }),
        new("typed-checkbox-readonly", 1, 0, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.CheckBoxCell(typedBool, true, true); sum += cell.Value == true ? 1 : 0; } return sum; }),
        new("typed-checkbox-editable", 1, 1, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.CheckBoxCell(typedBool, false, true); cell.Value = null; sum += cell.Value is null ? 1 : 0; } return sum; }),
        new("constant-text", 17, 0, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.TextCell<int>(17); sum += cell.Value; } return sum; }),
        new("constant-checkbox", 1, 0, count => { long sum = 0; for (var i = 0; i < count; ++i) { using var cell = new UI.CheckBoxCell(true); sum += cell.Value == true ? 1 : 0; } return sum; }),
    };
    const int count = 4096, warmups = 12, sampleCount = 25;
    var samples = new List<Sample>();
    foreach (var workload in workloads)
    {
        for (var i = 0; i < warmups; ++i)
        {
            var writes = TotalWrites();
            Verify(workload, workload.Run(count), TotalWrites() - writes);
        }
        for (var iteration = 0; iteration < sampleCount; ++iteration)
        {
            var writes = TotalWrites();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var checksum = workload.Run(count);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var written = TotalWrites() - writes;
            Verify(workload, checksum, written);
            samples.Add(new(workload.Name, iteration, count, elapsed, allocated, checksum, written));
        }
    }
    var report = new
    {
        schemaVersion = 1, revision = args[0],
        librarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(UI.TextCell<int>).Assembly.Location))),
        runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(), serverGc = GCSettings.IsServerGC,
        tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),
        count, warmups, sampleCount,
        scope = "Public scalar cell construction, synchronous initial delivery, optional writeback and disposal. Reused caller-owned publishers and leases; no formatting, native controls, complete grid, startup or GPU timing.",
        samples,
    };
    File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_SCALAR_SUBSCRIPTION_BENCHMARK_PASSED=" + args[1]);
    return 0;
    long TotalWrites() { long writes = 0; foreach (var source in sources) writes += source.Writes; return writes; }
    void Verify(Workload workload, long checksum, long writes)
    {
        if (checksum != count * workload.Value || writes != count * workload.Writes)
            throw new InvalidOperationException(workload.Name + ": incorrect value or writeback count");
        foreach (var source in sources)
            if (source.Active || source.Created != source.Released)
                throw new InvalidOperationException(workload.Name + ": leaked or double-released observer");
    }
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

internal sealed record Workload(string Name, long Value, int Writes, Func<int, long> Run);
internal sealed record Sample(string Operation, int Iteration, int Count, double Milliseconds, long AllocatedBytes, long Checksum, long Writes);
internal interface ISource { long Writes { get; } long Created { get; } long Released { get; } bool Active { get; } }
internal sealed class Source<T> : IObservable<T>, IObserver<T>, ISource
{
    private readonly T _initial;
    private readonly Lease _lease;
    private IObserver<T>? _observer;
    public long Writes { get; private set; }
    public long Created { get; private set; }
    public long Released { get; private set; }
    public bool Active => _observer is not null;
    public Source(T initial) { _initial = initial; _lease = new(this); }
    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (Active) throw new InvalidOperationException("Concurrent benchmark cells are not expected.");
        ++Created;
        _observer = observer;
        observer.OnNext(_initial);
        return _lease;
    }
    public void OnNext(T value) { ++Writes; _observer!.OnNext(value); }
    public void OnError(Exception error) => throw error;
    public void OnCompleted() { }
    private sealed class Lease(Source<T> owner) : IDisposable
    {
        public void Dispose()
        {
            if (!owner.Active) throw new InvalidOperationException("Lease released twice.");
            owner._observer = null;
            ++owner.Released;
        }
    }
}
