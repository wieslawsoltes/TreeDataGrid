using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;

if (args.Length != 2) { Console.Error.WriteLine("Usage: <revision> <output.json>"); return 2; }
var resources = new List<IDisposable>();
var cleanupChecks = new List<Action>();
try
{
    const int count = 8192, warmupBatches = 8, samplesPerWorkload = 15;
    var scenarios = new List<Scenario>();
    var samples = new List<Sample>();
    AddScalar("scalar-int-identity", 123456789);
    AddScalar("scalar-double-identity", 12345.625);
    AddScalar("scalar-decimal-identity", 12345.625m);
    AddScalar("scalar-string-identity", new string('x', 64));
    AddScalar<int?>("scalar-nullable-identity", 123456789);
    AddScalar("scalar-int-formatted", 123456789, "[{0:N3}]");
    AddScalar("scalar-custom-provider", 123456789, "{0}", new FormatterCulture());

    var row = new Row();
    var source = new FlatTreeDataGridSource<Row>([row]);
    resources.Add(source);
    var definition = new ValueColumn<Row, int>("Number", x => x.Number);
    source.Columns.Add(definition);
    var liveColumn = new UI.TextColumn<Row, int>(definition,
        new UI.TextColumnOptions<Row> { Culture = CultureInfo.InvariantCulture });
    var coreColumn = new ValueCellColumn<Row, int>(definition, CellKind.Text,
        new TextCellOptions { Culture = CultureInfo.InvariantCulture });
    resources.Add(liveColumn); resources.Add(coreColumn);
    var live = liveColumn.CreateCell(source.Rows[0]);
    var core = coreColumn.CreateCell(source.Rows[0]);
    resources.Add(live); resources.Add(core);
    scenarios.Add(new("live-int-identity", () => ((UI.ITextCell)live).Text, "123456789"));
    scenarios.Add(new("core-int-identity", () => ((UI.ITextCell)core).Text, "123456789"));
    scenarios.Add(new("boxed-column-format", () => liveColumn.FormatValue(row.Number), "123456789"));
    cleanupChecks.Add(() => { if (row.Subscribers != 0) throw new InvalidOperationException("Native model observers leaked."); });

    foreach (var scenario in scenarios)
    {
        for (var i = 0; i < warmupBatches; ++i) Verify(Run());
        for (var iteration = 0; iteration < samplesPerWorkload; ++iteration)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            var checksum = Run();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Verify(checksum);
            samples.Add(new(scenario.Name, iteration, count, elapsed, allocated, checksum));
        }
        long Run()
        {
            long checksum = 0;
            for (var i = 0; i < count; ++i)
            {
                var text = scenario.Read() ?? throw new InvalidOperationException("Unexpected null display.");
                checksum += text.Length;
                if (text.Length != 0) checksum += text[0];
            }
            return checksum;
        }
        void Verify(long checksum)
        {
            var expected = (long)count * (scenario.Expected.Length + (scenario.Expected.Length == 0 ? 0 : scenario.Expected[0]));
            if (checksum != expected || !string.Equals(scenario.Read(), scenario.Expected, StringComparison.Ordinal))
                throw new InvalidOperationException(scenario.Name + ": incorrect formatted content or checksum.");
        }
    }
    DisposeAll();
    foreach (var check in cleanupChecks) check();
    var report = new
    {
        schemaVersion = 1, revision = args[0],
        librarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(UI.TextCell<int>).Assembly.Location))),
        runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        os = RuntimeInformation.OSDescription, serverGc = GCSettings.IsServerGC,
        dynamicCodeCompiled = RuntimeFeature.IsDynamicCodeCompiled,
        tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),
        count, warmupBatches, samplesPerWorkload,
        workloadChecksums = scenarios.ToDictionary(s => s.Name,
            s => (long)count * (s.Expected.Length + (s.Expected.Length == 0 ? 0 : s.Expected[0]))),
        cleanupPassed = true,
        scope = "Warm public numeric TextCell and retained live/immutable native cell-model display queries, with unchanged string, nullable, custom-format, custom-provider and object-valued controls. Not native visual layout, frame rate, GPU completion, construction or startup.",
        samples,
    };
    var path = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("UNO_NUMERIC_FORMATTING_EXECUTION_PASSED=" + path);
    return 0;

    void AddScalar<T>(string name, T value, string format = "{0}", CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        var observable = new Once<T>(value);
        var cell = new UI.TextCell<T>(observable, true,
            new UI.TextColumnOptions<Row> { Culture = culture, StringFormat = format });
        resources.Add(cell);
        scenarios.Add(new(name, () => cell.Text, string.Format(culture, format, value)));
        cleanupChecks.Add(() => { if (observable.Subscribers != 0) throw new InvalidOperationException(name + ": observer leaked."); });
    }
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
finally { DisposeAll(); }

void DisposeAll()
{
    for (var i = resources.Count - 1; i >= 0; --i) resources[i].Dispose();
    resources.Clear();
}

internal sealed record Scenario(string Name, Func<string?> Read, string Expected);
internal sealed record Sample(string Operation, int Iteration, int Count, double Milliseconds, long AllocatedBytes, long Checksum);
internal sealed class Once<T>(T value) : IObservable<T>, IDisposable
{
    internal int Subscribers;
    public IDisposable Subscribe(IObserver<T> observer) { ++Subscribers; observer.OnNext(value); return this; }
    public void Dispose() => --Subscribers;
}
internal sealed class Row : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _handlers;
    public int Number => 123456789;
    internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
}
internal sealed class FormatterCulture() : CultureInfo("en-US"), ICustomFormatter
{
    public override object? GetFormat(Type? type) => type == typeof(ICustomFormatter) ? this : base.GetFormat(type);
    public string Format(string? format, object? value, IFormatProvider? provider) => "custom:" + value;
}
