using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace TreeDataGridBenchmarks;

// Load each revision, including its Avalonia dependencies, into an isolated context.
// Rotate measurement order in one process to reduce machine-load and warm-up drift.
internal static class RevisionScrollComparison
{
    public static void Run(string[] paths)
    {
        var revisions = new List<Revision>();
        var results = new List<object>();
        var workloads = new (string name, int operations)[]
        {
            ("VerticalRowScrolls", 10_000),
            ("HorizontalSmallScrolls", 6_000),
            ("HorizontalColumnScrolls", 1_000),
            ("HorizontalFarColumnScrolls", 1_000),
        };
        using var process = Process.GetCurrentProcess();
        try
        {
            foreach (var path in paths)
                revisions.Add(new Revision(Path.GetFullPath(path)));

            foreach (var workload in workloads)
            for (var round = -3; round < 9; ++round)
            for (var order = 0; order < revisions.Count; ++order)
            {
                var index = (round + 3 + order) % revisions.Count;
                var revision = revisions[index];
                revision.Invoke("Setup" + workload.name);
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var cpu = process.TotalProcessorTime;
                    var timer = Stopwatch.StartNew();
                    var rows = revision.Invoke(workload.name);
                    timer.Stop();
                    cpu = process.TotalProcessorTime - cpu;
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (round >= 0)
                    {
                        results.Add(new
                        {
                            Workload = workload.name,
                            Round = round,
                            Revision = index,
                            revision.Path,
                            Microseconds = timer.Elapsed.TotalMicroseconds / workload.operations,
                            CpuMicroseconds = cpu.TotalMicroseconds / workload.operations,
                            AllocatedBytes = allocated / (double)workload.operations,
                            RealizedRows = rows,
                        });
                    }
                }
                finally { revision.Invoke("Cleanup" + workload.name); }
            }
        }
        finally
        {
            foreach (var revision in revisions)
                revision.Invoke("GlobalCleanup");
        }
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class Revision : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly object _benchmark;
        private readonly Dictionary<string, MethodInfo> _methods = new();

        public Revision(string path)
        {
            Path = path;
            _resolver = new(path);
            var type = LoadFromAssemblyPath(path).GetType("TreeDataGridBenchmarks.VirtualizationBenchmarks", true)!;
            _benchmark = Activator.CreateInstance(type)!;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                _methods[method.Name] = method;
            type.GetProperty("NeutralSource")!.SetValue(_benchmark, true);
            Invoke("GlobalSetup");
        }

        public string Path { get; }
        public object? Invoke(string name) => _methods[name].Invoke(_benchmark, null);

        protected override Assembly? Load(AssemblyName name) =>
            _resolver.ResolveAssemblyToPath(name) is { } path ? LoadFromAssemblyPath(path) : null;

        protected override IntPtr LoadUnmanagedDll(string name) =>
            _resolver.ResolveUnmanagedDllToPath(name) is { } path ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
