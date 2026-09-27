# Unicode input and transactional row-replacement recovery

2026-09-27 UTC. Recovery commit `9e3de136`; tested implementation `b8618268`.
[Current checklist](uno-current-work.md) · [Machine-readable checkpoint](uno-ci-checkpoint-b8618268.json)

## Exact recovery, not a second implementation

The branch started this continuation at `91c3128013ea628e3bfab406d727f18625774737`.
Its native sample still failed to compile because `RangeReplacementRuntimeChecks`
used FocusManager without importing Microsoft.UI.Xaml.Input. A preceding interrupted
continuation had already prepared a reviewed patch as content-addressed blobs, but
had not published the product tree. The successful blob-materialization workflow
was not product validation and did not fix the branch merely by being green.

The source archive and pending patch were downloaded and hashed locally. Applying
the patch to the independently reconstructed source produced the exact prepared
Git tree `ed94decf99e6cd6e415766125dcdfa745dddfd60`. That tree was published as
`9e3de136b6cb99cc13aa65ba2f20c14496e5b2e5`, removing the temporary contents-write
materialization workflow. No force push or replacement privileged helper was used.
The source archive SHA-256 is
`5195f542a1730b62b1c294c382e64402d051523daf07d8c4d3ffb790fd697287`;
the recovered patch SHA-256 is
`0f6a2e96e728c138609f52bf74cfe23075d4601b27d5b05aef576d8c59e91f02`.

The subsequent geometry/index corrections were published as
`b861826818b3e9bc53ce1888e202275141e51511`, tree
`46cea6719c24f32a5464664684fb82badb07eee6`. Canonical CI merge
`c6ee890afa7239cd6d6eaebf562e4d49cda55389` has that identical tree. Local synthetic
commits used while reconstructing the archives are not represented as remote history.

## Recovered browser test discovery and Unicode behavior

The previous three-engine workflow used unittest discovery with hyphenated module
names. That can execute zero matching tests despite a successful process exit. The
new file-location runner loads every `test-uno-browser*.py` module explicitly and
rejects missing or empty modules, colliding module names, loader errors, incomplete
execution, skipped cases and failing tests. Thirty-six browser-driver test methods
actually execute. These are separate from the thirty-six recovered Uno unit cases.

The browser driver now inserts committed Unicode text through the browser keyboard
API, rather than treating arbitrary text as individual keyboard keys. The native
sample requires exact writeback of:

```text
Browser edited — Zażółć gęślą jaźń 日本語 🌲 👩‍💻
```

The existing fifteen pointer/keyboard stages, cancellation, tri-state sorting and
DPR 1/2 assertions remain. No application method is called to simulate a successful
edit. A successful committed-text insertion is not proof of physical keyboard or OS
input-method behavior.

Native incremental search now decodes valid surrogate pairs across character events,
rejects malformed UTF-16 and unsuitable control input, preserves complete text
elements, and gives a reentrant newer input request precedence over an older search.
Timeout and cyclic index arithmetic do not rely on overflowing signed subtraction
or addition. No source, selection or row model is copied into search state.

The loaded text-search suite verifies supplementary scalars, extended grapheme
cycling, multi-character committed input and reentrant search precedence. Its
`UNO_RUNTIME_UNICODE_SEARCH_PASSED` marker is present in canonical isolated and
sequential native logs.

## Composition-aware editing boundary

On heads implementing TextCompositionStarted and TextCompositionEnded, an editor
observer tracks the native composition lifecycle. It is allocated only when an
actual editor is used, not for each display-only cell. Both built-in text editors
and focused template TextBoxes participate; nested grids/cells retain their own
input ownership. Unsupported event stubs are excluded by the existing linker-visible
capability mechanism.

While composition is active or settling, Enter/Escape and grid navigation do not
finish the grid transaction prematurely. The end event queues completion through
the native dispatcher so its terminating key remains part of composition. A
session/realization generation invalidates completion callbacks from a retired or
replaced editor. Template changes, cancellation and cell retirement detach the old
observer. Focus-loss commit is deferred until composition ends where necessary.

The state-machine and text-input tests exercise these transitions and stale tickets.
Native search execution and browser committed-text insertion test different layers.
No real OS IME session, physical device, external screen reader, or complete branded
Safari/Firefox acceptance is claimed by this recovery.

## New transactional geometry correction

The range implementation staged count-changing sparse replacements, but routed
all equal-count replacements through sequential invalidation. For a partial
multirow range that is not safe: restoring early estimated heights can overflow
before a later large measurement is removed, even though the final state is finite.
It can also mutate an earlier entry before rejecting a genuinely overflowing final
extent.

Partial equal-count multirow replacements now use the same staged height-map and
Fenwick-index reconstruction as count-changing replacements. Both indexes and the
final exposed extent are validated before publication. Unaffected prefix/suffix
measurements survive. Uniform, single-row and whole-range equal-count fast paths
remain; no native model callback is added.

The exact same three new tests were first executed against the recovered original
runtime: two failed and one passed. One failure reproduced partial mutation after
an exception; the other reproduced rejection of a finite final extent. All three
pass with the correction, without changing their assertions. This is executed
failing-baseline evidence, not a claim inferred from compilation or source review.

The deliberate cost is staged dictionaries for partial sparse multirow replacements.
That is a correctness tradeoff; it is not a whole-grid speedup. Ordinary single-row
replacement and uniform geometry do not acquire this additional scratch storage.

## New retained-index overflow correction

A retained focus/bring-into-view suffix index was calculated with
`checked(capturedIndex + newCount - oldCount)`. The intermediate addition can
overflow even when removing the old range yields a valid final index. The new
internal helper widens the complete transformation before checking its final result:

```csharp
checked((int)((long)index - removedCount + addedCount))
```

Six cases cover valid near-limit remapping, genuine final overflow and 4,096
independent BigInteger-oracle combinations. A loaded existing range scenario also
exercises small/4,000-row growth and shrink, retained native focus, bounded control
counts, empty/repopulate, reentrant provider replacement, correct current-row
writeback and complete provider/model subscription cleanup. It uses actual native
containers over a custom Core-row provider. Its marker passes canonical isolated
and sequential execution; it is not a new registered suite.

## Validation and API accounting

Canonical run [36306250769](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250769)
passes all nineteen stages on unchanged committed sources. Actual TRX counters are
250 Core, 1,111 Uno, 536 original Avalonia, 41 sample-state and 319 paired-framework:
**2,257 cases, zero failed/skipped**. All 65 registered native suites, sequential
execution and Activity Monitor's five sections/lifetime checks pass. The downloaded
report ZIP was independently verified as
`ea2125a32ea99d4ddee4b01504870142f80452a963292f5763522a2b596fd719`.

Local complete test/native results agree. Local compilation used archived public SDK
10.0.201 with environment-only reference/apphost pack metadata adjustment from
8.0.25 to the installed 8.0.31, an offline package configuration and disabled workload
resolution for desktop builds. The first resolver failure and interrupted all-suite
shell wrapper are preserved alongside successful complete executions. Canonical CI
passes independently without those local environment adjustments.

The current compiled inventory has 1,845 reference / 1,903 target declarations,
1,064 exact matches, **781 missing-or-different** and 839 additional-or-different.
Dependencies resolve and normalization collisions are zero. The 590 identical Core
dependency records are not independently ported UI contracts. Strict target
self-comparison is clean. Supplemental differences remain: 13,465 / 15,944 entries,
3,890 exact, 9,575 missing-or-different and 12,054 additional-or-different.

The preservation gate reports no removed target declaration, rewritten previous
raw record or lost reference match relative to the pinned reviewed floor. The
three resolved matches versus the old 784-gap checkpoint belong to earlier committed
Text/CheckBox/Template TryReuseCell declarations, not new declarations authored in
this recovery. Width policies and typed factory return/virtual-slot adaptations
remain explicit native differences; they are not normalized away.

## Performance and acceptance limits

Independent native run [36306250796](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250796)
completes both builds and all four measured hosts, but fails the unchanged 1.10
median timing/allocation budget. Uno/Avalonia timing ratios are 2.127 horizontal,
2.309 vertical, 3.521 diagonal, 1.587 replacement, 1.472 resize and 1.470 sorting.
Allocation ratios are 0.695, 1.957, 1.917, 2.668, 1.518 and 0.620 respectively.
The independently verified artifact hash is
`35e8a4f99691e7989893e132153b3e2d0de2ea7c3ed5bd739c41661074f2eab4`.

These are synchronous UI/layout and settlement measurements, not GPU completion,
frame rate or a controlled before/after gain from the recovery. No benchmark or
normalization threshold was relaxed. Current platform and three-engine execution
states are recorded separately in the checkpoint; successful publication alone is
not browser execution. Full API, behavior and performance parity remain open.

## Actual three-engine results and unresolved Firefox findings

[Engine run 36306250857](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250857)
completed publication of one 686-file trimmed NuGet-consumer bundle. All engine
reports carry the same manifest hash
`7597ff44d7f1f441214a0feb9c7294e66db658d343b411c4fc1ef79e48aca3fe`
and the exact CI merge revision. Chromium 143.0.7499.4 and Playwright WebKit 26.0
each pass showcase, Activity Monitor, and all fifteen input stages at DPR 1 and 2.
The archived logs explicitly contain the loaded range/Unicode search markers and
exact Unicode-edit marker on both successful engines. These are engine-version
results, not certification of all branded browsers.

Firefox 144.0.2 passes Activity Monitor and the complete DPR 1 input route, but
showcase times out at 180 seconds and DPR 2 never reaches the asserted committed
edit state within the application's existing thirty-second phase budget. Showcase
logs reach horizontal recycling before the timeout. The DPR 2 trace records the
Control+A, Unicode insertion and Enter operations; it does not establish why the
edit remains active. The pinned Playwright Firefox insertion implementation uses
`commitCompositionWith`, making composition/terminating-key ordering a concrete
investigation lead, not a demonstrated root cause. No retry, timeout increase,
engine substitution, assertion skip or success waiver was used.

The three-engine aggregate correctly fails: **10 of 12 routes passed is not a
passing matrix**. The original traces and failures remain in artifact `10927637813`,
whose independently recomputed SHA-256 is
`ee9dd422a075353bf88b78c37dd13e1f1ab511222fb44405033de2471f34c388`.
The downloadable compact evidence includes reports, logs and screenshots; it does
not redistribute the trace's fetched fonts or the browser/toolchain binaries.

The ordinary platform workflow has five successful jobs, including its actual
Chromium consumer execution. macOS job `108583524591` remains queued with no steps;
no prior-revision result is substituted. The final documentation-only commit uses
`[skip ci]` to avoid replacing that queued product run under the existing PR
concurrency policy. It does not make new-head checks green or skip implementation
validation. PR #26 remains draft, with no merge or public release.
