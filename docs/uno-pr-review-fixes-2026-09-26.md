# PR #26 review fixes and reproduced contracts

2026-09-26 UTC. Baseline `5be0e5638cfd6be70ffe88839b695b790f013235`.
Tested correction `23f033eef3d1a6243f130d14b62ccc9e82bf7359`.
[Execution checkpoint](uno-review-fixes-checkpoint-23f033ee.json) ·
[Current checklist](uno-current-work.md) · [Reproduction guide](uno-api-preservation.md)

## Review scope

These changes address the six concrete findings in the preceding review. Earlier
declarative ownership and browser-validation work already present at the baseline
is preserved, not counted again. Fixing these findings does not prove the absence
of every other bug or complete native/Core/API/performance parity.

Four runtime files change: HierarchicalRow.cs and SortableRowsBase.cs in shared
Core; TreeDataGridSourceExtensions.cs and TreeDataGridRowsPresenter.cs in Uno.
Public signatures and the original Avalonia implementation remain unchanged.

## F1: hierarchy disposal must release every independent owner

HierarchicalRow.Dispose previously called the model-event remove accessor before
entering its cleanup chain. A throwing accessor prevented expansion observation,
child-reference observation and materialized descendants from being released after
the row was already marked disposed. Nested finally blocks could replace errors.

The corrected method retires the row and detaches child ownership first. It then
independently attempts model removal, expansion lease disposal, children-reference
lease disposal and descendant disposal in that order. A single failure preserves
its identity and stack; multiple errors retain ordered aggregates. An application
aggregate is not flattened into a different exception identity. Recursive disposal
cannot repeat cleanup and caller-owned models/collections are not disposed.

Model and children-reference fields are cleared before invoking their respective
remove/dispose callbacks. Reentrant expansion can install replacement observation
without an obsolete outer assignment clearing it after cleanup returns.

The observer interfaces' prohibition on synchronous change notification during
subscription remains intact. This correction handles cleanup callbacks and does
not claim concurrent access is safe or forcibly repair a publisher refusing removal.

## F2: reset must detach old storage and attempt every sibling

SortableRowsBase now detaches materialized rows and the sorted-index map before
calling row cleanup. One throwing sibling no longer prevents later releases.
Nested reset cannot rediscover the same retired list, and replacement rows created
by a callback are not erased by the returning outer reset.

The committed Reset is published even after cleanup errors so observers do not
retain an obsolete projection. A publication error follows cleanup failures in
the aggregate. Sorted and unsorted reset use the same protocol. Ordinary add,
remove and replace algorithms are not rewritten or certified by this correction.

The exception contract is post-commit reporting, not rollback: a cleanup exception
does not mean the removed collection remains current. Callers must not blindly
repeat a mutation at an old index. Healthy cleanup does not allocate an error list.

## F3: compare callbacks through the actual public factory

CreateCommonOptions already matched the reference's live callback policy, but
ApplyCommonOptions captured one delegate. Helper-only tests therefore missed the
production fluent factory's different behavior.

A factory now installs a wrapper only when its comparison is initially present.
That wrapper reads the caller's current callback whenever invoked. Later callback
replacement affects a previously obtained comparison. A throwing callback preserves
its original exception; clearing it retains the reference NullReferenceException.
An initially absent comparison remains absent in its old options snapshot.

Twelve direct-framework cases exercise the public text, Boolean, nullable Boolean
and template-resource-key factories: eight direction/lifetime cases and four
initially-absent cases. These are actual factory tests, not substituted calls to
the protected helper. Core source, sorting and selection identities stay unchanged.

## F4: native refresh cannot resume through retired owners

RefreshStyles now captures realized and pooled row lists before any Style property
callback. It validates source/presenter generation after each callback. A separate
style request revision ensures a nested style change within the same source wins.
Rows no longer parented by that presenter are ignored.

The same-presentation SetPresentation branch snapshots the same ownership lists
and validates after SynchronizeColumns. Nested replacement and layout cannot leave
an outer enumeration walking mutable dictionaries or updating retired controls.
New rows receive current configuration through their normal realization path.

This adds bounded snapshot allocations to style/column configuration refresh,
not ordinary viewport scrolling. It is a correctness tradeoff, not a speedup claim.

ReviewPresenterRuntimeChecks operates on a loaded 100-row grid and actual old/new
Core sources. It registers public DP callbacks for style-driven source replacement,
nested style precedence, and source replacement during column/selection refresh.
It checks multiple rendered row/model/text identities, editor writeback after
recovery and complete retirement without disposing either caller-owned source.
The existing appearance suite runs it in isolated and sequential entry routes;
package/browser consumers use that same suite. The marker is
UNO_RUNTIME_REVIEW_PRESENTER_CALLBACKS_PASSED. No new suite is registered and no
reflection into private presenter fields is used.

## F5: declared API regression is now a separate permanent gate

Generating a coherent audit or passing target self-comparison does not prove
preservation of previously exported APIs. The new schema-7 gate compares all
reference and target declaration records, not just counts. It rejects removal or
change of any old target normalized shape, changes to existing raw/owner records,
and loss of a former reference match even if another export keeps counts equal.
Non-reference target exports are protected too.

Reference records and normalization policy must be unchanged. Missing/malformed
dependency lists, duplicate JSON keys or normalized entries, unknown declaring
assemblies, absent/malformed hashes, bad scope accounting and forged counts fail
closed. Binary hash changes without declaration changes are permitted and recorded.
The production reader source tree itself is pinned across baseline and candidate;
an intentional reader/reference/floor update requires explicit maintainer review.

Sixteen gate tests and one preservation-join test protect the validator. The full
canonical run also mutates a real compiled inventory: it replaces one exact export
while retaining the target count. That control must be rejected. This is explicitly
an inventory-level negative control, not an emitted replacement assembly.

The runner overlays only the 22 new Core and twelve new public-factory tests into
the pinned baseline test projects. Runtime source is unmodified. All 34 must execute
without skipping; candidate cases must pass and baseline defects must actually
fail. Overlays are removed and tracked source cleanliness is verified.

The canonical aggregate now requires nineteen stages and joins actual TRX outcomes,
all native registrations and suite-specific markers, review-scenario markers,
the API positive gate and its rejected negative control. Fetching full history
and allowing extra build time changes no API or performance acceptance threshold.
This gate preserves declarations; it does not waive supplemental metadata or prove
ABI, native behavior, physical input or full port parity.

## F6: evidence regenerated instead of transcribed

The original erroneous b38ca0f7 checkpoint is retained verbatim under archive.
Its corrected record preserves the historical platform-status snapshot as historical.
The read-only repair workflow recomputes all four original hosts, every workload,
median, p95 and per-pass median and compares them with the original collector.
It checks exact revision/library identities and checksums and hashes every extracted
file. Output includes full Markdown tables and JSON provenance/manifest. Nothing
is rebenchmarked and no slow sample is dropped.

Correct historical values: live/Core formatted integer allocation 72/72 bytes;
live-integer candidate pass medians 54.736328125/53.9794921875 ns; 51 extracted files;
reader tree d391854a1ef4c51f34b87837e286152b1430b3a6; initial failed artifact
10913688104 with its original size and SHA256. Eight renderer and eight committed-
record tests reject forged summaries, incomplete hosts, altered fingerprints and
transcription errors. The checked-in numerical record must match derived evidence.

Historical artifact availability remains a reproduction dependency. Expiration is
a failed retrieval, not permission to substitute another revision's measurements.
The original erroneous record remains available for audit; corrected records and
this checkpoint are authoritative for the repaired values.

## Executed results and limits

The identical 34 fixtures on baseline execute 7 passing and 27 failing cases
(Core 3/19, paired 4/8). On the corrected runtime all 34 pass. Earlier missing
ModelIndexPath and nullable-flow errors in the new fixture were corrected without
weakening assertions; those compile failures are not counted as reproduced defects.

Canonical validation completes 2,178 tests, 65 native suites and nineteen required
stages. All six platform jobs complete, including macOS and published-browser
execution. The exact run IDs, hashes and distinction between verified native
markers and browser job-step evidence are in the execution checkpoint.

The independent 1.10 native performance gate still fails, and 784 declared API
plus supplemental differences remain. Source/frame/IME/accessibility and broader
callback acceptance is not inferred from passing these scenarios. No whole-grid
speedup, complete API parity, merge or release is claimed.
