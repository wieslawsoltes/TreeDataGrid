# Uno width, expander and nested-binding validation

Date: 2026-09-23 UTC. PR #26, `codex/uno-core-port`.
Starting head: `b6965095a8927cdc02aab1dac22985acf15da457`.
Tested product: `9e03ce28f082580bab71df633046622844d0ce58`.
Tested merge: `ca0c590f1756c3e022d992d180e00f3857cd36b0`.
Tested tree: `ed5d18b28986fe2a1ef4ad5a57dad42e4ebbac72`.

This is an implementation/evidence checkpoint, not certification of full parity.
The actual Core assembly and model ownership remain shared with Avalonia. The PR
remains draft; no merge, public package release or dependency-framework patch occurred.

## Changes and executed regressions

### Snapshot star-width calculation

`4771e51f` replaces repeated application-facing input reads in `ColumnWidths` with
a numeric snapshot. Each list element, model width and min/max constraint is captured
once per span-overload call. Getter/indexer failure leaves caller output unchanged;
calculation completes in independent scratch storage before publication.

The scratch state contains four doubles and no managed references. Up to 128 states
use at most 4 KiB of stack; larger inputs rent per-invocation storage and return it in
finally. Nested calculations cannot overwrite the outer scratch state. Maximum-wins
constraint precedence and the existing unmeasured Auto discovery floor are preserved.
Surviving star weights are renormalized after saturation, including extreme ratios
which would otherwise underflow. The active maximum is gathered during existing
passes rather than rescanning framework/model getters.

Sixteen new tests cover once-only evaluation, throwing/invalid constraints, throwing
indexers, changing getters, nested calculation, pooled reuse and null validation.
**Eleven failed on the original solver and all sixteen pass after correction.**
All prior randomized/large-weight/constraint and warmed-allocation regressions pass.
Application callback count is linear; the constrained numeric iteration itself is
not claimed to have a linear worst-case bound.

### Public observable expander cell

`cf094b0a` adds `Uno.Controls.Models.TreeDataGrid.ExpanderCell<TModel>` with the actual
borrowed `IExpanderRow<TModel>`, public content/state properties, editable-content
forwarding, observable expansion/visibility inputs and deterministic disposal.
Expansion writes go through the Core row controller; there is no copied hierarchy.
Framework-specific binding-expression inputs map to the existing public observable
convention used by TextCell/CheckBoxCell. This is not an exact metadata signature match
to Avalonia's framework-specific constructor.

The cell owns content and subscriptions, not the row/source. Cleanup continues across
throwing event removal and disposables, preserves/aggregates original exceptions, and
rejects retired callbacks before they mutate the row. Native custom-cell adapters now
surface expander-level observable errors as well as inner-cell errors.

Eight new unit cases verify content/row identity, real Core row-count expansion,
notifications, callback-time disposal, constructor failures, multiple cleanup failures,
observable errors and native adapter ownership. `4a772d8a` adds a real native-control
suite: rendering and geometry, live observable values, expansion writeback, inner text
editing, borrowed-model unrealization and source usability after cell disposal. The
same test executes in the sequential native and published browser consumers.

### Allocation-free nested owner access on JIT hosts

`9e03ce28` stops forcing expression interpretation for cached nested owner accessors
on runtimes which compile dynamic code. It selects interpretation when
`RuntimeFeature.IsDynamicCodeCompiled` is false, keeping the browser/AOT path without
introducing a runtime dynamic-code requirement there. The expression cache remains
weakly keyed and shared; existing reentrancy/lifetime boundaries are unchanged.

Two tests warm 1,024 retargets and measure 4,096 alternating nested-model retargets on
the JIT host. Before the fix, the property path allocated **622,592 bytes (152/call)**
and the collection-indexer path **1,277,952 bytes (312/call)**. Afterward both allocate
**zero managed bytes on the measuring thread**, preserve values and release previous
root/leaf subscriptions. These focused invariants do not prove full-grid allocation
or NativeAOT performance parity.

Official API contracts: [RuntimeFeature.IsDynamicCodeCompiled](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature.isdynamiccodecompiled)
and [Expression.Compile(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile).

### Trimmed browser wrapping and reproducibility

`9a809163` preserves precisely the private runtime-only `Item.Text` getter used by
`WrappingTemplate`. The starting browser run failed because that name-based binding
had no generated metadata rooting the fixture endpoint. The assertion was not replaced
with an estimated-height check, nor was trimming disabled. Both published browser
consumers now execute successfully, including all sequential wrapping assertions.

`6904590f` adds a read-only reference-pack snapshot workflow. It archives public
reference packs and the net8 runtime, never runner homes, credentials, configuration
or fonts. This supports offline validation without retargeting shared net8 Core.
The previously proposed ColumnGeometry indexed-list snapshot correction was already
in the starting branch and is not counted as a change in this continuation.

## Same-process solver experiment

The diagnostic runs the exact preceding solver and new solver against real built
CellColumn instances in one process. It verifies equal output, warms 2,000 calls,
then uses eight alternating AB/BA batches of 10,000 calls per solver. Counters record
actual min/max getter calls; thread-local managed allocation is measured separately.
The harness uses the existing test friend-assembly identity solely to reach the
internal solver. It does not change the production visibility/API surface.

| Workload | Old median ns | New median ns | New / old | Old / new constraint reads |
| --- | ---: | ---: | ---: | ---: |
| 64 fixed columns | 2,199.4 | 2,086.4 | 0.949 | 128 / 128 |
| 64 star columns | 4,128.2 | 1,783.7 | 0.432 | 128 / 128 |
| 64 constrained columns | 14,090.8 | 2,809.2 | 0.199 | 332 / 128 |
| 512 constrained columns | 116,968.7 | 23,510.2 | 0.201 | 2,686 / 1,024 |

Both solver variants allocate zero managed bytes after warm-up in these scenarios.
Raw batches and the executed harness are retained in the local evidence archive.
These are solver-only measurements, not native layout, GPU, frame-time or all-feature
performance acceptance. Startup compilation cost is excluded from the warmed nested
accessor measurements and has not been established equivalent.

## Final CI validation

[Functional run 35854319565](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35854319565),
artifact `10747056977`, validates unchanged merge `ca0c590f` containing product `9e03ce28`:

| Test group | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 210 | 0 | 0 |
| Uno | 399 | 0 | 0 |
| Avalonia | 536 | 0 | 0 |
| Sample state | 36 | 0 | 0 |
| Total | **1,181** | **0** | **0** |

All **41/41 registered native suites** pass with success markers and exit code zero.
Sequential integration, independent measurement recovery, both native sample builds,
Activity Monitor's five sections and lifetime checks pass. Both sample builds have
zero warnings/errors. All twelve functional validation exit codes are zero.

[Platform run 35854319634](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35854319634)
completed successfully: three-OS desktop builds/tests, Linux native/package consumers,
Windows App SDK samples/package consumers, browser builds/trimmed publication and
**actual execution of both published consumers in Chromium**. Browser job
`107159091907` preserves artifact `10747410472`. Repository Build `35854319615` and
published trimmed-binding contract `35854319598` also pass.

The continuation adds 26 unit cases and one native suite relative to the starting
branch. Other improvements already on that starting branch are not attributed here.
The old failed browser report and pre-fix unit failures remain diagnostic evidence.
The final CI result does not certify other browser engines, physical hardware input,
IME, screen-reader behavior, all DPI scales or general NativeAOT acceptance.

## Latest paired native performance: still failed

[Run 35854319556](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35854319556),
artifact `10746788587`, completes both hosts and four alternating AB/BA processes on
one runner, with 64 columns and 25 iterations per workload. The unchanged **1.10
median time/allocation budget fails**.

| Workload | Avalonia median ms | Uno median ms | Uno / Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.54330 | 2.68740 | 4.95 |
| Vertical scroll | 1.13790 | 2.71655 | 2.39 |
| Distant diagonal scroll | 2.72580 | 10.81135 | 3.97 |
| Replace visible row | 2.63130 | 3.64775 | 1.39 |
| Resize visible column | 4.80690 | 8.61320 | 1.79 |
| Sort | 42.97320 | 67.12390 | 1.56 |

Median Uno allocations are respectively 26,040; 196,608; 995,904; 102,816; 132,528;
and 1,062,136 bytes. Sorting allocates less than the paired Avalonia workload, but is
slower. Raw p95 and settlement measures are preserved in the machine checkpoint.
Only each framework comparison is paired on one machine: separate hosted revision
runs are not controlled before/after speedups. No overall timing improvement is
inferred from the isolated width/nested-binding experiments.

Scope is synchronous UI work and verified layout settlement, not GPU completion,
frame rate or physical input latency. A previously generated stack profile at
`42ca565c` was also reviewed, not regenerated at this final revision. It highlights
native layout/composition damage work, but includes waits/startup and is not CPU-only
attribution or proof of the final revision's bottleneck proportions.

## API inventory and remaining acceptance

Both metadata dependency sets resolve completely. The audit reports 1,748 baseline
and 1,592 target shapes, 864 exact normalized matches, 884 missing-or-different and
728 additional-or-different entries. Strict identical-assembly comparison has zero
differences. Categories remain 201 changed declarations, 55 absent exported identities,
117 members not declared on a matching type, 381 members of absent identities and
130 overload/parameter differences.

Those are declaration counts, not feature-completion percentages. Intentional Core
relocations, inherited/native signatures, generated Avalonia exports and genuine
omissions need explicit decisions backed by compatibility tests. Full API equivalence
and native performance parity remain false. Required work still includes those public
contracts, the measured layout/rebinding gaps, variable-height mixed-mutation timing,
other browsers, physical pointer/keyboard/drag-drop, Unicode/IME, screen readers, DPI
and repeated multi-head lifecycle/render reliability.

## Local execution boundaries

The local source tree was reconstructed from the exact GitHub source artifact and
verified against the committed tree. All identified workspace code changes are pushed;
there is no claim of access to unpublished files on the user's own machine.

Offline local validation used SDK 10.0.201, .NET 10.0.12, public NuGet snapshot packages,
and public net8.0.31 reference/runtime packs. The local SDK's net8 descriptor was pointed
to the available 8.0.31 pack; repository target frameworks and SDK files were not changed.
Offline-only restore flags disabled unavailable vulnerability-feed lookup and selected
offline certificate-revocation lookup. Original NuGet.config was restored before
source-tree verification. Those local flags are not a CI policy change or CI evidence.
Final acceptance results above come from unchanged-source GitHub runners.

The local archive preserves baseline failures, successful TRX/native logs, exact solver
harness/batches, source patch, environment notes and hashes. No SDK/package caches,
credentials or fonts are included. The prior checklist is archived byte-for-byte.
