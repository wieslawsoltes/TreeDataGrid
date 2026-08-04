# TreeDataGrid virtualization benchmarks

The BenchmarkDotNet suite measures the layout and allocation costs most affected by
TreeDataGrid virtualization:

- small vertical scroll updates;
- forced stationary vertical layouts and one-row scrolls, isolating layout reuse and
  recycling costs from scroll batching;
- buffered small vertical scroll updates;
- small horizontal scroll updates across many columns;
- collection insert/remove and move bursts inside the realized range; and
- repeated visual-tree detach/reattach cycles.

Run it from the repository root:

```sh
./benchmarks/run-benchmarks.sh \
  --filter '*' --exporters json --artifacts artifacts/benchmarks/current
```

Use the same machine, .NET runtime, build configuration, and benchmark commit when
comparing two TreeDataGrid revisions. Benchmark artifacts are intentionally ignored.

The virtualization benchmarks execute hundreds or thousands of layout operations per
iteration and report normalized per-operation results. This keeps each measured
iteration long enough to avoid drawing conclusions from sub-100 ms layout samples.
