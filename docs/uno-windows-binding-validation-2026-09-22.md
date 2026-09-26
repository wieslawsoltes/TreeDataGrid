# Uno native Windows and binding-lifetime validation

Date: 2026-09-22. PR #26, `codex/uno-core-port`.
This is a continuation checkpoint, not certification of complete parity.

## Native Windows build and package publishing

Commit `4d392872f2520ee5cb86f9891652393b26086918` moves the Activity Monitor
identity DataTemplate from Application resources to `IdentityCellResources.xaml`.
The dictionary has an x:Class, code-behind InitializeComponent call, and an explicit
instance in App's merged dictionaries. The resource key, layout, x:Load and OneWay
compiled bindings remain unchanged. The native XBF WMC0612 error is gone.

The next Windows compile reached generated XamlTypeInfo and exposed CS9035:
its activation expression calls `new MetricSeries()` without required initializers.
Commit `a69db7314c0d5d35054f003094afb26f4cf52f8b` adds a SetsRequiredMembers
constructor that actually initializes every required member. It creates an empty
series with non-default ImmutableArray storage and a positive chart ceiling. It
does not edit generated code, remove the required declarations, or suppress the
compiler diagnostic. Three tests cover native-style activation, named empty
series, and telemetry initializers overriding the defaults.

Completed [Uno run 35747491998](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35747491998),
[Windows job 106812743366](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35747491998/job/106812743366),
records success for both Windows sample builds, Core/Uno packing (including the
Windows framework assets), and publication of both PackageReference sample
consumers. The three-OS desktop build/unit matrix also passes in this run.
These are CI-local packages/publication directories, not a NuGet release or merge.
Windows physical-input/accessibility/runtime-rendering acceptance remains separate.

## Cell-binding lifetime correction

Product commit `021489e692f11206c4f0a5790e94e856b788f721` corrects the actual
Uno presentation binding, with no changes to the shared Core or Uno framework.

The old Refresh implementation could publish a result after a getter disposed,
suspended, or retargeted the binding. Nested owner getters could attach a leaf
following retirement. An event add/remove callback could also reenter lifecycle
operations before SetOwner completed its bookkeeping, losing a subscription or
leaving a retired handler attached. Seven of the initial eight regression cases
failed against the original implementation before the fix was applied.

The corrected contract is:

1. Retarget and Suspend advance a revision, even when returning to the same model.
   Suspend clears the public value/error immediately.
2. Refresh serializes owner attachment/detachment with pending lifetime changes.
   A reentrant change is drained as the in-flight callback unwinds, without a
   timer, dispatcher post, new transaction object or captured row task.
3. Owner getter, subscription accessor, value getter and value equality boundaries
   check the revision before using or publishing application-produced state.
4. Selector results and exceptions remain local until the revision is verified.
   Notifications cannot observe a retired result followed by the current result.
5. A callback requesting disposal and then throwing still drains pending cleanup.
   The original failure is preserved, or combined with a separate cleanup failure.

The new CellBindingReentrancyTests contain 11 cases: getter Suspend/Dispose;
retarget-only-current publication; retired getter exceptions; event-add disposal;
event-remove retargeting; nested-owner retirement; non-recursive notification
retargeting; disposal followed by a throwing notification; same-row retargeting;
and reentrant value equality. Existing alias-owner, nested null recovery,
collection-indexer, writeback, suspension/GC and presentation tests still pass.

CellBindingAllocationTests adds a separate normal-path invariant: after warming
1,024 retargets, 4,096 alternating row retargets allocate **zero managed bytes on
the measuring thread** and leave exactly one current subscription. This is not a
claim of full-grid timing, rendering or overall performance parity.

## Local execution evidence

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Shared Core | 210 | 0 | 0 |
| Uno presentation | 227 | 0 | 0 |
| Avalonia control | 520 | 0 | 0 |
| Uno sample state | 32 | 0 | 0 |
| Total unit cases | 989 | 0 | 0 |

Both desktop samples build with zero warnings/errors. Activity Monitor passes
lifetime, snapshot/template identity, selection, sorting, filtering, last-row
bring-into-view and bounded-realization checks across CPU, Memory, Energy, Disk
and Network. Five 1024x640 captures were produced; the CPU capture was inspected.

All 30 TreeDataGrid isolated runtime suites pass. The first full invocation was
interrupted by the execution host after recording 23 product passes and the
framework-only failure. The seven uncompleted suites were run separately and
passed with exit code zero and their required success markers. This is complete
per-suite evidence across two invocations, not a successful aggregate run.

The native-layout-recovery probe still fails: a bare Control remains at two measure
calls and width 40 rather than the requested width 80 after a handled exception.
The sequential showcase was also rerun and still exits 1 after custom reuse:
column sizing sees zero realized rows and the stale 5,600-pixel extent. Later
sequential assertions remain unrun. The local evidence archive preserves these
failures rather than reporting a successful aggregate.

Local sources were restored from the exact GitHub snapshot and compared by Git
tree hash. SDK 10.0.201 and the public package snapshot were used on Linux/X11.
The container has .NET 10 rather than a .NET 8 runtime, so net8 unit assemblies
used DOTNET_ROLL_FORWARD=Major. The Avalonia xUnit v3 executable used the installed
SDK's Linux apphost explicitly; an initial missing-apphost build was not counted
as a test pass. Offline restores disabled only unavailable vulnerability-feed
lookup locally. Repository feeds, audit settings, trimming diagnostics, tests and
CI performance thresholds were not changed. Local flags are not CI evidence.

A complete CI run for `021489e6` was not available when this checkpoint was
recorded. The native Windows CI success above belongs to `a69db731`, not to an
untested future head. Re-read completed CI artifacts for subsequent revisions.

## Remaining acceptance

Browser samples still build, but trimmed package-consumer publishing fails on
reflection/preservation contracts in the binding paths and ReactiveUI/Rx/Uno
libraries. No trimming warning suppression or disabled-trimming workaround was
added. A dependency with verified exception-safe native measurement remains
necessary for the independent recovery and sequential integration gates.

The unchanged paired native performance budget remains 1.10 for median synchronous
time/allocation ratios. This continuation does not claim improved full-grid timings
or change the preceding benchmark evidence. Metadata API-difference classification,
physical pointer/keyboard/drag-drop, Unicode/IME, accessibility, scaling and
multi-head rendering checks remain open. The PR stays draft and unmerged.
