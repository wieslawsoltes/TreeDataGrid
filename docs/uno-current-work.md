# Current Uno completion checklist

Updated 2026-09-23 UTC. Tested product **5339693a**.
**Complete API, all-feature and performance parity remain unproven.**

[Binding implementation report](uno-binding-write-storage-2026-09-23.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-5339693a.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-5339693a.md)

## Revision and ownership

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual `TreeDataGrid.Core` assembly remains shared with Avalonia. Sources,
rows, hierarchy and selection are not copied into a second model layer.
No merge, release, dependency change, new friend access, diagnostic suppression
or change to the 1.10 paired performance threshold was performed.

Starting commit: `0f1cec1725162e3927bbe4473fbb7a6a841bdccb`.
Tested product: `5339693abbfd2452b8ab6c34c3a7d48dbd9240f2`.
Product tree: `505036d6388cc304ecfb30f24d0f2779abfcbdcf`.
Tested merge: `5b163f7a160e0bb03952b69778bcfd88a16e1ca6`.
Documentation-only follow-ups do not change the tested implementation.

All implementation changes authored here were pushed directly through GitHub.
Local shell/Python execution returned ClientError, so unknown local working-tree
files could not be enumerated or certified. CI executed unchanged committed code.

## Completed functional validation

[Run 35906105813](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35906105813),
job `107334493909`, artifact `10771560898`, completed successfully:

| Gate | Result |
| --- | --- |
| Shared Core | 210 passed |
| Uno presentation | 477 passed |
| Avalonia control | 536 passed |
| Sample state | 36 passed |
| Total unit cases | **1,259 passed; zero failed/skipped** |
| Registered native suites | **49/49 passed** |
| Sequential showcase / native measurement recovery | Passed |
| Both native desktop samples | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| API dependencies / strict self-comparison | Fully resolved / zero self-differences |

The functional artifact's SHA-256 is
`118fc07ed34f9134875060826cfd95de81da2afea972a7f901175ff53746fd85`.
This is final-code execution, not a union of earlier partially completed runs.
The initial binding-only commit passed independently; a superseded intermediate
run was cancelled and is not reported as aggregate success.

[Platform run 35906106060](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35906106060)
contains separate desktop, Linux-native/package, Windows App SDK and published
browser-consumer gates. Their exact completion states, including the actual
browser execution step, are recorded in the linked machine-readable checkpoint.
Published artifacts are not by themselves browser-runtime evidence. The browser
configuration is pinned Chromium; it does not certify every browser or physical
input/accessibility/DPI stack. Repository Build `35906105816` and the separately
published/executed trimmed-binding contract `35906105798` completed successfully.

## New fixes and storage optimization

- A changed same-type exception now publishes the bound cell's current diagnostic
  through the existing Value/Error notification order. Identical error instances
  remain quiet, and comparison invokes no exception-defined Message/Equals code.
  If a setter and its final refresh both fail, both exceptions survive in order;
  single failures remain unwrapped and successful paths allocate no error list.
- Object conversion now belongs to the binding's original lifetime and write
  revision. IConvertible, enum ToString and provider callbacks cannot recycle a
  cell and write the older result to its new row. Newer nested typed/converted
  assignments win. Inactive/read-only operations do not execute user conversion.
  Nullable, enum and culture behavior retain the existing runtime path.
- Flat bindings store their root owner inline and share stateless root accessors,
  removing per-cell singleton arrays. Additional nested owners still have isolated
  storage. Root/child aliases maintain one subscription per distinct owner, and
  retained suspended bindings do not keep released models alive.
- Public integration checks exercise the actual presentation pool, not a simulated
  retarget. The same seven scenarios run as `binding-write-contract` and in the
  sequential native/trimmed-browser consumer. Exact subscription cleanup and
  caller-owned shared Core sources are asserted.

This adds **27 unit cases and one native suite with seven runtime scenarios**.
The new allocation checks pass: zero allocation in warmed write paths, and no more
than 256 managed bytes per warmed root-only binding construction including its
two event delegates. This construction budget is not a promise of zero allocation
or an exact object size on every runtime.

## Performance and API acceptance remain failed/unproven

[Paired run 35906105859](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35906105859),
job `107334088486`, artifact `10770294728`, completed both builds and all four
AB/BA host processes with valid frames. The unchanged **1.10 median timing and
allocation-ratio budget failed**. Synchronous time ratios: 7.97x horizontal,
2.10x vertical, 4.22x diagonal, 1.47x visible-row replacement, 1.78x column resizing
and 1.49x sorting. Raw medians/p95 and scope are in the report and artifact.

No overall grid speedup is established by comparisons against earlier jobs on
different hosted machines. Focused allocation checks and eliminated allocations
are not substitutes for whole-grid acceptance. These benchmark results measure
synchronous UI work and layout settlement, not GPU completion or frame rate.

The compiled audit remains 1,748 baseline/1,599 target declarations, 865 exact
normalized matches and 883 missing-or-different baseline entries, with zero
unresolved types. The changes are internal binding corrections, not new exported
API shapes. Core relocations, native signatures, inherited members, generated
exports and actual omissions still require explicit tested equivalence decisions;
these counts are not feature-completion percentages.

Remaining acceptance: genuine public-contract gaps; native timing/allocation
budgets; broader variable-height/mixed-mutation benchmarks; repeated multi-head
runtime validation; physical keyboard/pointer and positive drag/drop; Unicode/IME;
screen readers and DPI coverage. Passing current fixtures does not close those
unrelated requirements.

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite binding-write-contract
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

Later completed artifacts supersede this checkpoint. Pending or cancelled jobs
are never counted as successful final-head execution.
