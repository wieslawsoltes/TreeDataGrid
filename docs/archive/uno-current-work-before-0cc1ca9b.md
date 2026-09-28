# Current Uno completion checklist

Updated 2026-09-27 UTC. Tested implementation **b8618268**.
**Full API/performance parity is not established. Firefox's new matrix is failed;
macOS remains queued. Canonical functional validation passes.**

[Recovery and implementation review](uno-unicode-range-recovery-2026-09-27.md) ·
[Exact checkpoint](uno-ci-checkpoint-b8618268.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-b8618268.md)

## Recovered and newly implemented work

The branch began at `91c3128013ea628e3bfab406d727f18625774737`, whose sample still
failed to compile. A prepared Unicode/composition patch existed as verified Git
blobs but was not yet part of the product branch. Recovery commit
`9e3de136b6cb99cc13aa65ba2f20c14496e5b2e5` publishes its exact reviewed tree and
removes the temporary write-enabled materialization workflow. Both input archives,
the patch and reconstructed source trees were independently checked locally.

Recovered changes include the FocusManager import; actual discovery of hyphenated
browser-test modules with empty/skip/error rejection; committed Unicode browser
input; scalar-safe surrogate handling and grapheme/reentrant search; and native
composition-aware editor key/focus lifetime. Composition observers are scoped to
actual editor sessions and reject stale deferred callbacks. This does not certify
physical OS IME or external accessibility.

New product commit `b861826818b3e9bc53ce1888e202275141e51511` makes partial multirow
equal-count geometry replacement transactional and widens retained suffix-index
arithmetic before its final checked conversion. Two tests reproduce actual original
runtime failures: mutation before an overflow exception, and rejection of a finite
final extent. The same three geometry tests then pass. Six index tests include
near-limit cases and 4,096 independent arbitrary-precision oracle comparisons.
Single-row, uniform and whole-range geometry fast paths remain unchanged.

The tested tree is `46cea6719c24f32a5464664684fb82badb07eee6`. CI merge
`c6ee890afa7239cd6d6eaebf562e4d49cda55389` has that identical tree. Newly authored
coverage is nine Uno cases; thirty-six additional Uno cases and browser/native
coverage are recovered work, not counted twice. No registered native suite was
added. Earlier column declarations and count-changing replacement are also preserved,
not represented as newly authored in this recovery.

## Canonical execution and local reproduction

[Canonical run 36306250769](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250769)
passes all **nineteen stages** on unchanged committed source. Actual main TRX totals:
**250 Core + 1,111 Uno + 536 original Avalonia + 41 sample-state + 319 paired-framework
= 2,257 tests**, zero failed or skipped. All **65/65 native suites**, sequential
native execution, both native builds with zero warnings/errors, and Activity
Monitor's five sections/lifetime checks pass. The range-replacement and Unicode
search markers occur in isolated and sequential native logs.

Thirty-six Python browser-driver test methods now actually execute. The old
hyphenated-file discovery issue is not represented as preexisting test coverage.
The downloaded canonical report, recovery artifacts and native performance report
were independently hashed. Report artifact `10927920624` preserves all raw evidence.

Local complete test/native results agree, but local builds used documented offline
SDK/reference-pack adjustments. The initial resolver failure and interrupted shell
wrapper remain preserved; a complete fresh native run passes all 65 suites.
Canonical CI succeeds independently without these local environment changes.

## Platform and engine results are separate

[Platform run 36306250790](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250790)
has **five successful jobs out of six**: Ubuntu and Windows builds/tests, Linux
native/NuGet consumers, Windows App SDK build/publication, and actual published
Chromium consumers. macOS job `108583524591` is still queued with no executed steps;
it is not reported as passed or replaced with older evidence.

[Three-engine run 36306250857](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250857)
publishes one hash-identified consumer bundle for all engines. Chromium
143.0.7499.4 and WebKit 26.0 each pass all four routes, including fifteen real
browser-input stages at DPR 1/2 and exact Unicode writeback. Their published showcase
logs also confirm range-replacement and Unicode-search checks.

Firefox 144.0.2 passes the monitor and complete DPR 1 input route. Its showcase
hits the existing 180-second timeout; its DPR 2 input route fails the thirty-second
commit-edit assertion. The aggregate correctly fails with **10/12 routes passed**.
Both failures and traces remain in artifact `10927637813`; no retry, timeout increase,
engine fallback or relaxed assertion was applied. Composition/terminating-key
ordering is an investigation lead, not a proven root cause or fixed failure.

Native Windows publication is not Windows runtime execution. Engine tests do not
prove branded-browser, physical keyboard/IME, external screen-reader or all-DPI
acceptance. The documentation-only follow-up uses `[skip ci]` to avoid superseding
queued product macOS validation, not to make new-head checks green. No merge is
requested and no product/test commit or acceptance rule was skipped.

## API preservation passes, but 781 differences remain

The unchanged compiled auditor reports 1,845 reference / 1,903 target declarations,
1,064 exact matches, **781 missing-or-different** and 839 additional-or-different.
The shared Core dependency contributes 590 identical records. Independent UI totals
are 1,255 / 1,313 / 474 exact / 781 missing / 839 additional. Dependencies resolve,
normalization collisions are zero and strict self-comparison is clean.

Preservation against the reviewed floor finds no removed export, rewritten old raw
record or lost reference match. The three resolved matches versus the historical
784 count are earlier Text/CheckBox/Template TryReuseCell declarations, not new
exports from this recovery. Native width and typed factory adaptations remain
explicit differences. Supplemental metadata is 13,465 / 15,944 entries, 3,890 exact,
9,575 missing-or-different and 12,054 additional-or-different. No waiver is added;
counts and dependency self-matches are not feature-completion percentages.

## Unchanged performance and remaining acceptance

[Native run 36306250796](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36306250796)
completes both builds and all four measured hosts but fails the original **1.10**
timing/allocation budget. Time/allocation ratios are: horizontal **2.127/0.695**,
vertical **2.309/1.957**, diagonal **3.521/1.917**, row replacement **1.587/2.668**,
resize **1.472/1.518**, and sorting **1.470/0.620**. Raw p95 and settlement data remain
in artifact `10927641839`; these are not frame rate or a controlled before/after
speedup experiment.

Remaining work includes unresolved API/native/Core/inheritance contracts, Firefox's
two execution failures, native text/layout and source-sort costs, broader hierarchy/
variable-height performance, the earlier intermittent allocation observation,
physical input/drag, Unicode/IME, external accessibility and cross-head acceptance.
Staged partial replacement adds bounded scratch storage for correctness, not a
performance claim. Shared Core ownership, renderer settings, API normalization,
existing assertions and native performance thresholds were not weakened.

All authored product changes are pushed. PR #26 remains draft; no merge or release.
