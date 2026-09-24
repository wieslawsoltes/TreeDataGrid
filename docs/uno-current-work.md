# Current Uno completion checklist

Updated 2026-09-24 UTC. Tested product **527a8644**.
**Full API, behavioral and performance parity are not established.**

[Built-in comparison audit and implementation](uno-built-in-comparison-review-2026-09-24.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-527a8644.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-527a8644.md) ·
[Custom value-column guide](uno-custom-value-columns.md)

## Shared architecture and revisions

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Actual TreeDataGrid.Core sources, rows, hierarchy and selection remain shared with
Avalonia. The new native APIs do not replace Core sorting or change dependency,
rendering, trimming or release policy.

Starting head: `469e6f6605128bfaff271385b12c8d81b361e8ea`.
Product: `527a86443dd88afdf1cf0772498fa0fb9a9eae46`.
Product tree: `663ba1e44eab8487b5d1c8a58066275fcaa74453`.
Tested merge: `e7925b37be66cc4a5bdbe29fa65447c1b1ca7873`.
Documentation is committed after the completed product platform run and does not
alter the tested implementation.

## Verified implementation

`90108e63` restores built-in ValueSelector and GetComparison through ValueCellColumn.
Raw selectors use the original Core getter. Value comparisons capture initial
permissions and explicit delegates, lazily cache default delegates and return null
for invalid directions. Template comparisons read live explicit delegates without
fallback, resource lookup or implicit suppression by UI sorting permission. Core
source order remains independent when view options deliberately disagree.

`527a8644` adds the native/sequential builtin-column-comparison consumer: original
Core identities, raw versus formatted values, captured/live comparison policies,
native edit/re-sort, distant rendering, bounded realization and source cleanup.
It also executes in the published trimmed-browser route.

Nineteen new actual-framework differential cases exercise those contracts. The
warmed 4,096-iteration string comparison/query loop allocates zero managed bytes
on the executing thread. Cold compilation and application callback allocations
are not included; no overall native-grid speedup is claimed.

## Completed functional and platform evidence

[Functional run 35992479692](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35992479692),
job `107609596104`, passes all fifteen required stages on unchanged sources.
Artifact `10804807227` preserves full API inventories, fingerprints, TRX and logs.

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state tests | 228 / 594 / 536 / 41 passed |
| Direct framework comparisons | 87 passed |
| **Total .NET cases** | **1,486; zero failed/skipped** |
| Registered native suites | **58/58 passed** |
| Sequential showcase and measurement recovery | Passed |
| Both native sample builds | Zero warnings/errors |
| Activity Monitor | All five sections and lifetime checks passed |
| Python audit / metadata semantic / normalization checks | 12 / 39 / 57 passed |

Authored coverage is nineteen differential cases and one native suite. Existing
tests are preserved, not counted again as newly implemented work.

[Platform run 35992479554](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35992479554)
is fully successful: Windows/Linux/macOS desktop jobs, Linux native package-consumer
execution, Windows App SDK builds and publication, and browser builds, trimmed
publication and actual execution of both consumers. Existing browser input routes
also pass at device scales 1 and 2. Repository Build `35992479617`, reproducibility
`35992479624` and executed trimmed binding `35992479631` pass too.

Browser-dispatched input is bounded pinned-Chromium coverage, not physical hardware,
all browsers, IME, external screen-reader or universal DPI acceptance.

## Performance remains failed

[Paired run 35992479581](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35992479581),
artifact `10805160762`, builds both hosts and completes all four AB/BA processes
with valid frames. The unchanged **1.10 median timing/allocation budget fails**.
Uno/Avalonia timing ratios: **4.58x horizontal, 1.78x vertical, 2.42x diagonal,
1.41x row replacement, 1.28x resizing and 1.41x sorting**. Sorting allocates less
but remains slower. Raw medians/p95/allocation/settlement are preserved.

Scope is synchronous UI/layout settlement, not GPU completion or frame rate. The
standard source benchmark does not use the public view comparison utility. Different
hosted runs are not controlled revision comparisons; focused zero-allocation checks
do not replace whole-grid acceptance.

## Remaining acceptance

Audit: **1,845 baseline / 1,831 target declarations, 1,010 exact normalized matches,
835 missing-or-different baseline and 821 additional-or-different target entries**.
Dependencies resolve and strict self-comparison passes. Every raw difference and
supplemental inheritance/interface/attribute entry remains available. These are
not feature-completion percentages and no Core equivalences were automatically accepted.

Built-in columns still lack the reference ColumnBase mutable Binding descriptor
and protected factory, although the separate custom ColumnBase supports them.
Cells must genuinely consume descriptor changes with correct pooled lifetimes;
a disconnected property would not close this remaining extension/inheritance gap.
Inherited checkbox state and reusable-cell methods were not duplicated to alter counts.

Required before ready: finish genuine member/signature/inheritance/attribute
contracts and tested Core mappings; complete remaining semantic review; meet native
performance budgets with hierarchy/variable-height/mixed workloads; extend physical
input, Unicode/IME, drag, external screen-reader and scaling/lifecycle coverage.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite builtin-column-comparison
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

Authored implementation is pushed. Local shell/Python execution returned ClientError,
so unknown unrelated working-tree files could not be inspected. No assertion,
trimming diagnostic, ownership rule, rendering setting or threshold was weakened.
