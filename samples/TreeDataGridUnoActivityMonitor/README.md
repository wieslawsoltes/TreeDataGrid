# TreeDataGrid Uno Activity Monitor

The original PR #12 five-tab sample, ported onto the actual `TreeDataGrid.Core`
sources and `TreeDataGrid.Controls.Uno` presentation. No copied model layer or
Avalonia dependency. CPU, Memory, Energy, Disk and Network retain their tables,
numeric sorting, search, stable-key selection, inspectors and Skia trend charts.

## Run

```sh
dotnet run --project samples/TreeDataGridUnoActivityMonitor/TreeDataGridUnoActivityMonitor.csproj -c Release -f net10.0-desktop
```

macOS desktop uses read-only native telemetry (`libproc`, Mach, `getfsstat`,
`getifaddrs`). Other desktop systems use deterministic demo data. Pass `--demo`
to force demo mode on macOS. The sample does not terminate processes or modify
system settings. Data is displayed locally, not uploaded.

Native energy is an estimate, not Apple's private Activity Monitor score. Memory
percentage is estimated from used physical memory, not macOS memory pressure.
Per-process compression is unavailable from this provider and displays `—`;
wired memory is not mislabeled as compression. Storage totals describe the root
volume, avoiding double-counting shared APFS container capacity. Network counters
come from the 32-bit `getifaddrs` interface; resets/wraps produce a zero delta for
that sample rather than a huge rate. Native values are not a diagnostic authority.

## Validation

```sh
dotnet run --project samples/TreeDataGridUnoActivityMonitor/TreeDataGridUnoActivityMonitor.csproj -c Release -f net10.0-desktop -- --smoke --demo --screenshot-dir "$PWD/artifacts/uno-activity-demo"
# macOS only: the same assertions with live telemetry
dotnet run --project samples/TreeDataGridUnoActivityMonitor/TreeDataGridUnoActivityMonitor.csproj -c Release -f net10.0-desktop -- --smoke --screenshot-dir "$PWD/artifacts/uno-activity-live"
```

Success prints `UNO_ACTIVITY_LIFETIME_PASSED`, five
`UNO_ACTIVITY_SECTION_PASSED` markers, and `UNO_ACTIVITY_MONITOR_SMOKE_PASSED`,
then exits zero. Failures exit nonzero. Checks cover all five tables/charts,
numeric sorting, filters, inspector selection, replacement snapshot identity,
parent/template retention without detach/reattach, last-row virtualization,
right-aligned text, cancellation, late results, idempotent disposal and recovery
after provider errors. Provider/series tests are source-linked into
`TreeDataGridUnoSample.Tests`; UI assertions run in the native Uno head.

CI runs deterministic X11 smoke checks both with project references and with
freshly packed Core/controls packages. Local package consumption is available via
`-p:TreeDataGridUnoPackageVersion=<version>` and
`-p:RestoreAdditionalProjectSources=<absolute-package-directory>`.

## Architecture and limits

- Core owns columns, numeric comparisons, source rows and selection. Uno owns
  templates and immutable `TextCellOptions`; `Numeric` is a presentation key.
- Refreshes never overlap. Disposal cancels a capture, rejects late results,
  clears the five sources and releases the provider after capture completes.
  Unload stops the timer/pulse; reload creates a fresh shell and sources.
- Selection is restored by model key before Core source-reset notifications can
  discard it. Templates use OneWay compiled bindings for replacement snapshots.
- This checkpoint supports the Skia desktop head. Browser, Windows App SDK,
  mobile heads and OS-dispatched input parity are not claimed complete. The
  original platform files are references, not proof of a working migrated head.
- Control type-ahead search (separate from this sample's search box), drag/drop,
  full theme/API parity, accessibility and comparative performance remain on
  the [port checklist](../../docs/uno-port-status.md).
