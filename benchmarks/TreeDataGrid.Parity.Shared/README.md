# Paired native UI layout workload

This benchmark uses the original Avalonia control source through `TreeDataGrid.Avalonia`,
the Uno control, and **one shared workload implementation**. Both hosts use .NET 10,
10,000 Core rows, 64 columns (configurable to 1,000), DejaVu Sans 14, fixed 32-pixel rows,
128-pixel initial columns, an 800x480 body viewport, no column headers, hidden scrollbars
and zero viewport cache. These are intentionally restricted, comparable conditions.

Run on Linux with X11 dependencies, `xvfb-run`, `fc-match`, and DejaVu Sans installed:

```sh
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25
```

The runner builds both hosts, then executes separate native processes in AB/BA order
on the **same machine**, without running the hosts concurrently. Warmup is excluded.
Each mutation is checked for viewport geometry, fixed row/column sizes, actual Core
row identity, current displayed text and bounded realization before it is accepted.
A wrong layout, missing operation, timeout, mismatched runtime/configuration, missing
font, non-finite measurement or process failure is a failure, not a timing sample.

Operations: visible row replacement, visible column resize, sorting, vertical scroll,
horizontal scroll and distant diagonal scroll. `SynchronousUiMilliseconds` and
`SynchronousUiAllocatedBytes` bracket source mutation/scroll submission plus forced
layout on the UI thread; verification and asynchronous work are excluded from these
metrics. `SettledMilliseconds` includes verified layout settlement and its polling
and verification overhead; it is a diagnostic latency, **not GPU completion**.
Per-operation raw samples, medians, p95 values and Uno/Avalonia ratios are preserved.
The optional `--max-ratio` fails when either synchronous median time or allocation
exceeds the supplied ratio. CI requests a 1.10 diagnostic budget and preserves failing
results too. No slow sample is dropped. Hosted-runner noise requires repeated runs
on a controlled machine before interpreting small differences.

Passing this restricted workload does **not** prove complete performance parity.
It does not cover variable-height templates, images, headers, editing, hierarchical
expansion, text input, device scaling, physical input latency, frame pacing, render
thread CPU, GPU work, peak/retained memory, accessibility, or Windows/macOS/browser
behavior. The output deliberately keeps `completePerformanceParityProven` false.
