# Custom adapter write and cleanup lifetime review

2026-09-24 UTC, PR #26, `codex/uno-core-port`.
**Full API, behavioral and performance parity are not established.**

## Implementation and provenance

This continuation started at `c6d9dd2b98f00e2b3b632692414d75a10ae5294d`.
Incoming column-estimation, header/resize, content-layout, built-in Binding and
typed-root corrections were already present, preserved and revalidated. They
are not counted as authored work here.

| Commit | Authored work |
| --- | --- |
| `367fd56b82973c7125dc17c44a3e7b562565cad7` | Pending custom write-lifetime correction and fifteen unit cases |
| `ad18b10073e5eae31bbc1dfaa7b078cdab0ffa66` | Retained-row/native editor consumer, registered and executed sequentially |
| `6b94034435ba3e1fb411c0a5a8bf2f68700d0b5c` | Adapter subscription/ownership failure preservation and twenty-two unit cases |

Tested product: `6b94034435ba3e1fb411c0a5a8bf2f68700d0b5c`.
Product tree: `19d679958c39894e62b3ebe023da724dcc3b6e0d`.
Tested CI merge: `e5a10c3cabbe79fabd3cc6ba4244d62277e22640`.
Base: `3ca47316d724e5e040ab0281a880e8df999b25fc`.
The subsequent documentation-only commit does not change this implementation.

The actual shared Core assembly owns sources, rows, hierarchy and selection.
No duplicated source model, public release or merge was introduced. Local shell
and Python execution returned ClientError. All authored implementation was
committed through GitHub and executed on unchanged CI checkouts. Unrelated
unknown local working-tree files could not be enumerated or certified as pushed.

## Custom write transaction

Previously the adapter checked CanWrite, converted an object to text, then entered
the inner setter without checking operation ownership. A permission getter or
conversion callback could dispose the adapter, retarget its cell, or perform a
newer assignment. The returning old result could write into a replacement row or
overwrite the newer value.

The adapter now captures a write revision before permission evaluation and checks
it after permission and conversion callbacks. Disposed entry is rejected before
application code runs. Stale returning operations do not act on their old permission
result. Non-string/non-null conversion also rechecks mutable edit permission;
ordinary string edits avoid this second getter. CultureInfo.CurrentCulture, null
conversion, checkbox casting and unsupported-cell behavior remain unchanged.
Real conversion failures preserve their identity. Newer nested assignments win.

Custom TryReuseCell invalidates writes before invoking application code, including
same-model reuse and false/throwing returns after partial application mutation.
Adapter-owned expander descendants receive this invalidation. Borrowed native
CellValue implementations retain responsibility for their own lifetime. Successful
retarget and disposal invalidate again before metadata or cleanup callbacks.
No transaction object, model snapshot or deferred callback is allocated per write.
This does not roll back arbitrary application setter side effects or implement the
entire custom-expander content transaction.

Fifteen unit cases cover owned/borrowed retirement during permission/conversion,
nested writes, successful/rejected/throwing reuse, same-model reuse, disposed and
read-only entry, permission changes during conversion, exception identity and
recovery. The warmed string test executes exactly 4,096 writes with zero managed
allocation on the measuring thread. Setup and application allocations are outside
that measurement; it is not whole-grid or GPU performance evidence.

## Actual native and published-consumer regression

The `custom-write-lifetime` suite uses public APIs over 160 actual shared Core rows
and an application-defined ICellColumn/ITextCell. It is registered in App.Validation
and also runs through the sequential native and published trimmed-browser consumers.

A conversion callback replaces row zero and performs native layout. The test
requires the same native control, custom cell model and CellValue adapter, and
confirms that custom reuse actually occurred. Neither original nor replacement
row may receive the obsolete conversion. Old/new subscription counts and absence
of premature disposal are checked explicitly.

The fixture also verifies nested assignment ordering, conversion error identity,
new read-only state, native editor opening/commit without duplicate writeback,
permission/conversion-time source retirement, rejection after disposal, distant
row rendering and bounded realization. Final retirement requires one disposal of
every owned custom cell, zero row subscriptions and continued usability of the
borrowed Core source. It uses the existing native layout-settlement approach;
its elapsed durations are not a performance benchmark.

## Construction and cleanup failures

A custom event add accessor can attach a handler and then throw. The shared
attachment helper now attempts rollback and preserves the original add failure
plus any rollback failure. Owned-cell adaptation retains its existing outer
ownership cleanup. Failed column construction leaves the view with the creating
caller/factory instead of adding another disposal owner.

Retirement is recorded before detachment. Unsubscription failure no longer skips
owned-model disposal or disappears behind a second exception. Single failures
retain exception identity and dispatch information; multiple failures preserve
execution order, including nested construction/rollback/ownership stages.
Borrowed models are not disposed, recursive Dispose is idempotent, and a queued
column callback replayed by an event remover is ignored after retirement.

Twenty-two tests cover all owned/borrowed and removal/disposal combinations,
partial-attachment failure, rollback, recursive cleanup and queued notifications.
The fixtures detach before throwing. The library cannot force an arbitrary
application accessor to detach if it refuses; it preserves that failure and
attempts other owned cleanup. Further expander-specific content and cleanup review
remains open.

## Completed execution

[Functional run 36054939338](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36054939338),
job `107819614789`, passes all fifteen required stages with unchanged sources.
Artifact `10831833666`, SHA-256
`7a2307abd89605e0a90047cb4fd87a6c74e65f4f3f4b1a3e5fab70f2105bf734`,
preserves full inventories, source fingerprints, TRX and native logs.

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state cases | 228 / 695 / 536 / 41 passed |
| Actual-framework comparison assembly | 151 passed |
| Total .NET cases | **1,651; zero failed/skipped** |
| Registered native suites | **64/64 passed** |
| Sequential native showcase and measurement recovery | Passed |
| Both native sample builds | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| Python audit / metadata semantic / normalization checks | 12 / 39 / 57 passed |

Authored coverage is **37 Uno unit cases and one native suite**, with no new
direct-framework cases. Incoming estimator tests are included in the 151-case
comparison assembly but are not counted again. Superseded intermediate runs are
not treated as final acceptance.

[Platform run 36054939298](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36054939298)
completed successfully: Windows/Linux/macOS desktop jobs, Linux native package
consumers, Windows App SDK builds/package publication, browser builds/trimmed
publication, and actual execution of both published browser consumers. Browser
job `107819712289` reports four passed routes and zero failures: showcase, monitor,
showcase-input-scale-1 and showcase-input-scale-2. Documentation was pushed after
product-platform completion, not while browser acceptance was running.

Windows App SDK publication is not Windows OS runtime execution. Pinned Chromium
consumers and browser-dispatched pointer/keyboard tests at scales 1 and 2 do not
certify physical hardware, universal browsers, IME, external screen readers or
universal DPI behavior. The zero-warning statement above applies to native sample
builds, not every tool's output on every head.

Repository Build `36054939446`, contract reproducibility `36054939373` and the
independently published/executed trimmed-binding contract `36054939215` also pass.

## Whole-grid performance remains failed

[Paired run 36054939244](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36054939244),
job `107819422399`, completes both builds and all four AB/BA host processes with
valid frames and unchanged sources. The **1.10 median timing/allocation ratio
budget remains failed**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scrolling | 0.47120 | 2.18435 | 4.636 |
| Vertical scrolling | 0.88315 | 2.29685 | 2.601 |
| Distant diagonal scrolling | 2.11175 | 8.21690 | 3.891 |
| Replace visible row | 1.81055 | 2.79105 | 1.542 |
| Resize visible column | 2.90470 | 4.51120 | 1.553 |
| Sort | 29.60585 | 57.46930 | 1.941 |

Artifact `10832306592`, SHA-256
`22f805bdb7427f268d9441e72d7bc74a5778104cf4d195e4afca9638fb4b63d9`,
preserves raw allocation, p95 and settlement results. Sorting allocates less than
Avalonia here but remains slower. Scope is synchronous UI work and layout
settlement, not GPU completion or frame rate. The standard flat built-in workload
does not isolate custom conversion/reuse. No controlled before/after experiment
or overall grid speedup is claimed. Focused zero-allocation checks do not replace
the failed whole-grid acceptance gate.

## Compiled API and remaining acceptance

The unchanged auditor records 1,845 baseline / 1,833 target declarations, 1,010
exact normalized matches, 835 missing-or-different baseline entries and 823
additional-or-different target entries. Both dependency sets resolve completely;
strict self-comparison has zero differences. Supplemental metadata is 13,465
baseline / 15,732 target / 3,822 exact / 9,643 missing-or-different / 11,910
additional-or-different. Every raw difference remains preserved.

This continuation changes internal behavior, not public declarations. Core
relocations, native signatures, inherited contracts, interface maps and attributes
still require explicit tested equivalence decisions. Counts and an empty unmatched
owner category are not feature-completion percentages. Consumed built-in Binding
already exists; legacy combined inheritance and genuine extension contracts remain
under review.

Remaining acceptance includes further custom-expander/content and mixed-mutation
callback review; genuine member/signature/inheritance/attribute completion; the
unchanged timing/allocation gate with broader hierarchy/variable-height workloads;
and physical pointer/keyboard/drag, Unicode/IME, external accessibility and cross-head
scaling/lifecycle verification. No assertion, Core ownership rule, dependency,
trimming diagnostic, rendering option or performance threshold was weakened.

## Reproduction

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj -c Release \
  -p:TreeDataGridUnoTargetFrameworks=net10.0 \
  --filter 'FullyQualifiedName~CustomCellWriteLifetimeTests|FullyQualifiedName~CustomAdapterCleanupTests'
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite custom-write-lifetime
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
