# Column geometry and horizontal cell recycling: v11 native Core

Measured on 2026-09-05 against `codex/mvvm-core-v11` at
`b7868ee5c268b2d86c78218a87acf352ae8fdb8e`. The model remains framework-neutral;
all production changes are in the Avalonia assembly. The geometry-only revision is
`8362fa2a460f749113bd6db5cc4264f1063a417a`; the combined revision is the follow-up
recycling commit containing this report.

## Changes measured separately

1. **Geometry only:** cache cumulative actual column widths and the measured-width
   average in each view's column collection. Viewport lookup uses an upper-bound
   binary search. Width notifications, commits, and collection changes invalidate
   the cache. Unknown widths and zero-width column boundaries retain their existing
   behaviour. Column notifications use weak subscriptions.
2. **Combined:** also pool native text and checkbox cell models by view and column
   identity. Idle bindings disconnect their property paths and clear their row/value
   references. Retargeting reuses binding expressions and subscription machinery.
   The pool holds at most 256 models and 32 column buckets, evicts the least recently
   used buckets, and clears on presentation suspension/disposal and column changes.
   Templates, expanders, and unsupported custom cell models keep their disposal path.
   Public general-purpose observable bindings retain their existing root behaviour.

Column identity is recorded at realization, rather than inferred from an index that
may have changed after insertion or reordering. Core rows are still consumed directly;
no Core-to-legacy wrappers or model-owned layout/binding caches were introduced.

## Alternating native scrolling comparison

Apple M4, macOS 26.6.2, .NET 8.0.19 ARM64, Release, SDK 10.0.301. Each revision and
its dependencies were loaded in a separate assembly load context. The same native
`VirtualizationBenchmarks` methods ran in one process, rotating revision order over
three warm-up rounds and nine recorded rounds. Setup, cleanup, and explicit GC are
outside timing. Every recorded workload returned **21 realized rows**.

Mean elapsed microseconds per operation, with sample standard deviation:

| Workload | Baseline | Geometry only | Combined |
|---|---:|---:|---:|
| One-row vertical scroll | 34.86 ± 0.22 | 34.84 ± 0.41 | 35.15 ± 0.52 |
| One-pixel horizontal scroll | 19.52 ± 0.82 | 22.36 ± 7.61 | 19.77 ± 0.97 |
| Horizontal column scroll, 200 columns | 231.54 ± 4.73 | 216.49 ± 11.20 | 183.67 ± 3.22 |
| Horizontal column scroll near column 900, 1,000 columns | 425.02 ± 8.63 | 185.00 ± 2.41 | 159.48 ± 1.44 |

Geometry alone reduced the two column-scroll workloads by **6.5%** and **56.5%**.
Adding cell-model recycling reduced them a further **15.2%** and **13.8%** relative
to geometry alone. Combined reductions against the baseline were **20.7%** and
**62.5%**. Process CPU time also decreased: 237.51 → 222.35 → 190.04 μs for the
200-column workload, and 423.89 → 184.71 → 159.25 μs for the far-column workload.

Vertical and one-pixel scrolling do not show a clear speed improvement. The geometry
one-pixel series includes a 42.50 μs sample; it is retained in the mean above. Its
median was 20.10 μs. The combined vertical mean was 0.8% above baseline and combined
one-pixel mean was 1.3% above baseline; their sample distributions overlap.

## Allocation measurements

BenchmarkDotNet 0.15.8, five warm-up iterations and ten measured iterations.
Managed allocations per native operation (KiB = 1,024 bytes):

| Workload | Baseline | Geometry only | Combined |
|---|---:|---:|---:|
| One-row vertical scroll | 7.20 | 7.12 | 7.12 |
| One-pixel horizontal scroll | 1.92 | 1.91 | 1.89 |
| Horizontal column scroll, 200 columns | 168.19 | 167.37 | 102.69 |
| Horizontal column scroll near column 900 | 159.90 | 159.08 | 94.29 |

Combined column-scrolling allocations fell **38.9%** and **41.0%**. The alternating
runner independently measured the same allocation reductions. The legacy `Source`
path also benefits from geometry caching and the lighter cell-owned binding root,
but the per-view model pool is enabled for native `Model` presentations.

Pooling trades bounded retained binding objects for fewer allocations on revisiting
columns. Forward-only scrolling into unseen columns still creates their bindings;
the one-pixel workload shows that its allocation benefit is much smaller there.

## Direct geometry benchmark

`ColumnGeometryBenchmarks.LocateViewport` queries positions across 20, 200, and 1,000
fixed-width columns. Both versions allocate zero bytes per steady-state lookup.

| Columns | Baseline | Geometry only |
|---|---:|---:|
| 20 | 41.63 ns | 6.34 ns |
| 200 | 321.91 ns | 10.51 ns |
| 1,000 | 1,594.04 ns | 11.44 ns |

## Evidence and limitations

[Raw results](results/scroll-recycling-v11/) include the BenchmarkDotNet statistics,
individual measurements, alternating-run samples, and a manifest with assembly and
source hashes. Revision indexes in `alternating-scroll.json` are baseline (0),
geometry only (1), and combined (2). The final production sources were compared
byte-for-byte with the measured combined checkout.

An unrelated C++ build competed for CPU during the isolated geometry/combined
scrolling runs, so those elapsed times are preserved as diagnostics, not used for
the headline timing comparison. In those runs even unchanged paths varied widely.
The alternating comparison reduces exposure to that drift; it does not turn this
machine into an exclusive benchmark host. Absolute timings also differ between the
isolated and assembly-load-context runners. These are headless layout/recycling
measurements, not GPU-rendered frame-rate claims.

An initial comparison was discarded after discovering that BenchmarkDotNet could
resolve its project from the caller's checkout. `run-benchmarks.sh` now changes to
its own repository before launching BenchmarkDotNet. Recorded runs were checked
against the generated project's reference path. Fresh builds also needed explicit
Core project references in the benchmark and UI-test projects because this repository
disables synthetic transitive project references.

## Reproduction

Use separate clean checkouts for the baseline, geometry-only change, and combined
change. Copy the benchmark additions and explicit benchmark Core project reference
to the baseline; leave its production sources unchanged. Use the same runtime and
run one build/benchmark process at a time.

```sh
./benchmarks/run-benchmarks.sh --filter \
  '*ColumnGeometryBenchmarks*' \
  '*VirtualizationBenchmarks.VerticalRowScrolls*' \
  '*VirtualizationBenchmarks.HorizontalSmallScrolls*' \
  '*VirtualizationBenchmarks.HorizontalColumnScrolls*' \
  '*VirtualizationBenchmarks.HorizontalFarColumnScrolls*' \
  --exporters json --keepFiles --artifacts /absolute/result-directory
```

Then use the combined checkout's runner with the three built benchmark assemblies:

```sh
dotnet benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks/bin/Release/net8.0/Avalonia.Controls.TreeDataGrid.Benchmarks.dll \
  --compare-scroll-revisions \
  /baseline/benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks/bin/Release/net8.0/Avalonia.Controls.TreeDataGrid.Benchmarks.dll \
  /geometry/benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks/bin/Release/net8.0/Avalonia.Controls.TreeDataGrid.Benchmarks.dll \
  /combined/benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks/bin/Release/net8.0/Avalonia.Controls.TreeDataGrid.Benchmarks.dll
```

Validation: **462 UI tests and 110 Core tests passed** in Release. New tests cover
geometry invalidation/boundaries, weak column subscription lifetime, horizontal reuse
after column insertion, nested subscriptions, retargeted edits and checkboxes,
view/column isolation, eviction, removed-row collection, and detach cleanup.
`build/check-neutral-dependencies.py` passed, including the negative UI-reference gate.
