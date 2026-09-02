# Native Core API: Avalonia 12

Measured sequentially on 2026-09-02 with BenchmarkDotNet 0.15.8, macOS ARM64, .NET 8.0.19, Release, 5 warm-ups and 10 iterations. Both API paths use the implementation committed with this report (`12.0.0.7-mvvm.2`). No second benchmark or build ran concurrently.

The native path binds Core sources to `TreeDataGrid.Model`. Rows, sorting, expansion, and selection remain Core objects. UI columns contain binding/layout state and directly create cells from Core rows. Existing `Source` consumers use the compatibility presentation into this shared renderer. There are no Core-to-legacy model adapters.

| Workload | Legacy Source | Native Model | Allocated legacy / native |
|---|---:|---:|---:|
| One-row vertical scrolling | 33.03 μs | 36.30 μs | 7.2 KB / 7.2 KB |
| Move burst | 19.45 μs | 19.07 μs | 4.23 KB / 4.23 KB |
| Detach/reattach | 863.0 μs | 866.7 μs | 724.5 KB / 728.82 KB |
| Full creation and layout | 2,161.93 μs | 2,158.32 μs | 1721.11 KB / 1709.42 KB |

The initial native detach/reattach design recreated columns and bindings each time. On v11 that measured 1,099 us and 908.75 KB versus 858 us and 724.5 KB. Caching compiled expression delegates reduced it to 944 us and 762.44 KB. The final control retains its view-owned column/layout objects on detach, disconnects Core subscriptions, and synchronizes model changes when reattached. This removes the main regression: v11 now measures 864 us versus 847 us (+2.0%); v12 measures 867 us versus 863 us (+0.4%). Added detach allocation is 4.32 KB (+0.6%). Regression tests cover changed rows/widths during detachment and confirm notifications stop and resume correctly.

Full creation/layout is effectively unchanged in the paired measurements and allocates about 0.7% less than the compatibility path. Steady-state scrolling and collection allocations are unchanged. The v11 vertical result is +1.3%, horizontal +0.5%, while moves and insertion/removal are slightly faster. The first v12 vertical comparison was +9.9%; this result is retained below rather than hidden by averages from different runs.

The table uses the matrix for scrolling/edits, the subsequent creation run where available, and the final lifetime comparison for detach/reattach. The lifetime change is outside the timed scrolling/edit operations. Original branch measurements remain in [the earlier split report](MVVM_SPLIT_RESULTS.md), which records the original base revisions. This paired comparison isolates native and compatibility paths under the same current renderer.

[Result data](results/native-core-v12.json) preserve BenchmarkDotNet's complete CSV fields and distinguish the initial matrix, compiled-binding cache run where present, and final lifetime run. The initial v11 source-only diagnostics also remain there: native sorting directly uses Core accessors and avoids the legacy interpreted-comparison allocations. Those diagnostics preceded the binding cache and are not presented as final constructor measurements.

Reproduce using `dotnet run -c Release --project benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks -- --filter '*VirtualizationBenchmarks.VerticalRowScrolls*' '*VirtualizationBenchmarks.CollectionMoveBurst*' '*VirtualizationBenchmarks.DetachReattach*' '*VirtualizationBenchmarks.CreateAndLayoutGrid*'`. For v11 also include horizontal scrolling and insert/remove. Timing is local evidence, not a guarantee for every application or machine.

## V12 scrolling follow-up

The isolated v12 repeat measured 37.36 us legacy / 44.03 us native (+17.9%), after the matrix's 33.03 / 36.30 us. Allocation remained identical. A sampled-thread-time profile over 25 invocations per API attributed the work to the shared layout/anchoring/array-copy paths and showed approximately 10.10 / 10.20 seconds in the scrolling workload; profiling is diagnostic, not a replacement timing benchmark.

To control drift and warm-up differences across separate processes, the paired runner warms both APIs in one process, then alternates order across 10 samples per API, each containing the same existing 10,000-operation scrolling method. Setup, GC and cleanup remain outside the timed region. The result was 34.527 us legacy / 34.977 us native (+1.3%), identical 7,368.4-byte allocations, and 21 realized rows on both paths. No code changed between the slower separate-process repeat and paired comparison. This supports a small steady-state difference under controlled conditions, while retaining the earlier slower observations as a limitation of the isolated measurements.

Reproduce with `--paired-vertical-scroll` instead of BenchmarkDotNet filter arguments. The runner is checked in beside the existing benchmarks. Both the isolated repeat and paired samples are stored in the v12 result JSON. The v11 matrix independently measured a 1.3% vertical-scroll difference.
