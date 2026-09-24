# Current Uno completion checklist

Updated 2026-09-24 UTC. Tested implementation **6b940344**.
**Full API, behavioral and performance parity are not established.**

[Custom adapter write/cleanup review](uno-custom-write-review-2026-09-24.md) ·
[Exact completed execution checkpoint](uno-ci-checkpoint-6b940344.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-6b940344.md) ·
[Custom value-column guide](uno-custom-value-columns.md)

## Architecture and revisions

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core assembly owns sources, rows, hierarchy and selection.
No duplicated source model, public release or merge was introduced.

Starting head: `c6d9dd2b98f00e2b3b632692414d75a10ae5294d`.
Product: `6b94034435ba3e1fb411c0a5a8bf2f68700d0b5c`.
Product tree: `19d679958c39894e62b3ebe023da724dcc3b6e0d`.
Tested merge: `e5a10c3cabbe79fabd3cc6ba4244d62277e22640`.
Documentation-only updates do not change this product. Incoming column-estimation,
header/resize, content-layout, built-in Binding and root fixes were preserved,
not counted as authored implementation in this continuation.

## Implemented and executed

- The pending custom adapter write fix is committed: generation checks reject
  obsolete permission/conversion results and old writes after same/other-row reuse.
  Reuse invalidates before custom callbacks, including false/throwing returns;
  adapted expander descendants receive invalidation without gaining ownership of
  borrowed native values. Newer nested assignments win. Ordinary string writes
  retain the warmed zero-allocation path.
- The public `custom-write-lifetime` consumer exercises actual same-control/model/
  adapter reuse over 160 Core rows, native editor commit, conversion errors,
  changed permissions, source retirement, distant rendering and exact cleanup.
  It is registered and also runs through sequential published consumers.
- Custom event additions that throw after attachment are rolled back without
  losing original errors. Unsubscription failure no longer skips owned disposal
  or hides behind a second failure. Borrowed cells are never disposed. Recursive
  disposal and queued retired column notifications are guarded.

Authored coverage: **37 Uno unit cases and one native suite**. No new public API
shapes or direct-framework cases are claimed. Further custom-expander/content
review remains separate.

## Completed functional and platform evidence

[Functional run 36054939338](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36054939338)
passes all fifteen stages on unchanged sources: **1,651 .NET cases**, zero failed
or skipped, **64/64 native suites**, sequential showcase/recovery, both native
sample builds with zero warnings/errors, and five Activity Monitor sections plus
lifetime checks. Core/Uno/Avalonia/sample/direct-framework totals are
228/695/536/41/151. The 12 Python audit tests, 39 metadata-semantic and 57
normalization checks pass. Artifact `10831833666` retains all evidence.

[Platform run 36054939298](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36054939298)
is fully successful: three desktop jobs, Linux native package execution, Windows
App SDK build/publication, browser trimmed publication, and actual execution of
both published consumers. Browser job `107819712289` reports four passed routes,
zero failed, including browser-dispatched input at scales 1 and 2. Documentation
was pushed only after that completed. Windows publication is not OS execution;
pinned browser tests are not universal physical-input/IME/accessibility acceptance.
Repository Build, offline reproducibility and published trimmed-binding execution
also pass. Exact run IDs and checksums are in the checkpoint.

## Performance remains failed

[Paired run 36054939244](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36054939244)
completes both builds and all four AB/BA hosts with valid frames but fails the
unchanged **1.10 median timing/allocation ratio budget**. Uno/Avalonia timing ratios
are 4.64x horizontal, 2.60x vertical, 3.89x diagonal, 1.54x replacement, 1.55x resize
and 1.94x sort. Raw medians, allocation, p95 and settlement data are in artifact
`10832306592`. These are synchronous UI/layout measurements, not GPU completion
or frame rate. No controlled revision speedup is claimed; focused allocation tests
do not replace the whole-grid gate.

## Remaining gates

The compiled inventory remains 1,845 baseline / 1,833 target declarations, 1,010
exact normalized matches and 835 missing-or-different baseline entries, with zero
unresolved dependencies. Raw and supplemental metadata differences are preserved.
These are not feature-completion percentages or automatically accepted mappings.

Required: genuine member/signature/inheritance/attribute completion and explicit
Core equivalences; further callback/mixed-mutation and custom-expander review;
unchanged native performance budgets with broader hierarchy/variable-height
workloads; and physical input/drag, Unicode/IME, external accessibility and cross-head
scaling/lifecycle acceptance.

All authored implementation is pushed. Local shell/Python execution returned
ClientError, so unrelated unknown local working-tree files could not be inspected
or certified as pushed. No assertion, Core ownership rule, trimming diagnostic,
rendering option or performance threshold was weakened.
