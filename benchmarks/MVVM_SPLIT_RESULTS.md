# MVVM split: Avalonia 11

Measured sequentially on 2026-09-02, macOS ARM64, .NET 8.0.19, BenchmarkDotNet 0.15.8, Release, 5 warm-ups and 10 measured iterations. Baseline: `origin/release/11.x` at `b93f4ce0e69a501c8245b38fdc59ac6f11c0e739`. The existing virtualization workloads are unchanged; a parameter selects the new neutral source adapter. A creation-and-layout workload was added to both checkouts.

| Workload | Original baseline | Split legacy API | Neutral source + adapter |
|---|---:|---:|---:|
| Grid creation and full layout | 2,101 us | 2,108 us | 2,163 us |
| One-row vertical scrolling, repeat | 33.71 us | 32.92 us | 32.96 us |
| Horizontal column scrolling | 198.98 us | 197.99 us | 207.34 us |
| Insert/remove burst | 55.61 us | 54.60 us | 51.15 us |
| Move burst | 18.88 us | 18.91 us | 19.01 us |
| Detach/reattach | 841.12 us | 826.71 us | 840.47 us |

Scrolling allocations stay unchanged: 7.2 KiB per vertical operation and 168.19 KiB per horizontal operation. Detach/reattach stays at 724.5 KiB. Neutral insert/remove adds about 0.2 KiB and moves add about 0.3 KiB per benchmark operation (each includes two edits). Creation plus full layout adds about 10 KiB to 1,720.67 KiB, around 0.6%.

The full matrix recorded one slower neutral vertical-scroll result of 42.24 us. An isolated repeat, without code changes, measured 32.92 us legacy / 32.96 us neutral; the earlier matrix had also measured 33.38 us for neutral scrolling. All measurements are retained in [the result data](results/mvvm-v11.json). This machine is not an exclusive benchmark host, so these results establish a regression check, not an unconditional latency guarantee.

The source-only diagnostic separates fixed adapter setup from rendering:

| Workload | Legacy | Neutral + adapter |
|---|---:|---:|
| Two-column source and presentation creation | 3.683 us / 10.93 KiB | 4.875 us / 14.93 KiB |
| Sort 1,000 rows | 683.8 us / 3,145.9 KiB | 82.61 us / 4.16 KiB |
| Expand/collapse 1,000 rows | 8.332 us / 8.13 KiB | 7.789 us / 8.33 KiB |

The adapter adds a fixed 1.2 us and 4 KiB when constructing the two-column source alone. In the full control workload, creation and layout is about 3% above the original baseline. Sorting uses a cached compiled Core accessor. Both hierarchy benchmark paths subscribe to row changes so their event-payload costs are comparable.

Benchmarking found and corrected two issues: redundant selection invalidation on collection moves, and eager Core accessor compilation even when the UI could bind the expression directly. No rendering pipeline or existing virtualization workload was replaced.

Reproduce the rendering comparison with:

```sh
./benchmarks/run-benchmarks.sh --filter \
  '*VirtualizationBenchmarks.CreateAndLayoutGrid*' \
  '*VirtualizationBenchmarks.VerticalRowScrolls*' \
  '*VirtualizationBenchmarks.HorizontalColumnScrolls*' \
  '*VirtualizationBenchmarks.CollectionInsertRemoveBurst*' \
  '*VirtualizationBenchmarks.CollectionMoveBurst*' \
  '*VirtualizationBenchmarks.DetachReattach*' --exporters json
./benchmarks/run-benchmarks.sh --filter '*NeutralSourceBenchmarks*' --exporters json
```
