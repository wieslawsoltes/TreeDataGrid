# Current Uno completion checklist

Updated 2026-09-23 UTC. Tested product **90d5df7a**.
**Full API, all-feature behavior and performance parity are not established.**

[Detailed audit and implementation report](uno-parity-review-2026-09-23.md) ·
[Completed execution checkpoint](uno-ci-checkpoint-90d5df7a.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-90d5df7a.md)

## Architecture and exact evidence

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Both frameworks consume the actual shared TreeDataGrid.Core assembly. This
continuation does not modify Core or Avalonia product sources; it corrects native
contracts and adds independent audit/differential/runtime validation.

Starting head: `6f12ba1df4291665013ce90dee708e2dde8298cd`.
Tested product: `90d5df7a505af3dfadeb7883cbc346a6024cb597`.
Product tree: `34cf203865564290d84472ff3bbca90f914b4c97`.
Tested merge input: `c07b6d1b7f9bf1420bd9fd88af0ea0d20da773ff`.
The compiled comparison uses both actual framework libraries and Core built from
this tested checkout. Later documentation-only commits do not alter that product.

## Completed functional and platform validation

[Functional run 35918998366](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35918998366),
job `107377806542`, artifact `10776212933`, completed with all fifteen required
stages successful and the checkout unchanged:

| Gate | Passed |
| --- | ---: |
| Shared Core | 210 |
| Uno presentation | 527 |
| Avalonia control | 536 |
| Sample state | 36 |
| Direct framework contract comparisons | 14 |
| **Total .NET test cases** | **1,323; zero failed/skipped** |
| Separate Python audit tests | 12 |
| Registered native suites | **52/52** |

Sequential showcase, measurement recovery, both native sample builds, and all five
Activity Monitor sections/lifetime checks pass. Native sample builds have zero
warnings/errors. The complete raw audit, source fingerprints, TRX files, native
logs and stage outcomes are preserved in that artifact.

[Platform run 35918998361](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35918998361)
is **fully successful**, verified after its `2026-09-23T21:03:54Z` final update.
Windows/Linux/macOS desktop jobs, Linux native/package consumers, Windows App SDK
samples/package consumers, browser builds, packaging, trimmed publication and
**actual execution of both published browser consumers** all pass. Browser job
`107377806297` completed its execution step successfully. Documentation was not
pushed until this product run finished, avoiding cancellation of that acceptance.
Repository Build and the separately published/executed trimmed-binding contract
also pass. Browser scope is pinned Chromium, not every browser, OS input stack,
screen reader or DPI configuration.

## Audit findings and fixes

- Exhaustive accounting covers **all 1,748 compiled baseline declaration shapes**.
  Every raw difference and extra target declaration is retained. Same-name Core
  candidates are review leads, never automatically accepted equivalences. Twelve
  accounting tests fail on lost/invented/duplicate differences or inconsistent
  inputs. Tracked-source SHA-256/line inventories and explicit boundary scans are
  included; these are not a line-by-line semantic verification claim.
- Native text facade cells now consume mutable format, culture, alignment,
  wrapping, trimming, search and editing metadata. Immutable render snapshots are
  reused while options are unchanged. Current culture also reaches actual native
  editor conversion inside the existing binding lifetime transaction. Options are
  read on normal refresh/query, not through an invented automatic-change event.
- Text-column search now matches the reference raw ValueSelector result rather
  than display prefixes/numeric formats. Explicit search selectors still win;
  nulls remain null and display formatter callbacks are not invoked for raw search.
  The old contradictory test was corrected against Avalonia and keeps its exact
  display-format assertion independently; no test was removed or skipped.
- Missing protected custom presentation row/cell-selection hooks are implemented
  and connected to the real native grid event. Lazy snapshots, original Core model
  identity, observer removal, retirement and nested updates are tested. Built-in
  Core events are not duplicated, and cell deltas do not invent row selections.
- Row automation exposes the reference ShowsMenu=false contract, tested for
  realized, expanded and retired rows alongside existing native provider checks.

New coverage: **20 Uno unit cases, 14 direct two-framework cases, 12 Python audit
cases and two native suites**. The native suites also run in published sequential
consumers. Direct comparison executes actual Avalonia/Uno libraries over the same
Core rows, including defaults, null/Unicode/formatted text, three cultures, mutable
styles, binding/writeback/reuse and Pixel/Auto/Star geometry. It does not pretend
that fourteen cases certify every UI behavior.

Focused 4,096-iteration warmed allocation checks pass for unchanged option/text
queries, raw string search and unobserved cell hooks. These are not a whole-grid
performance result. Initial fixture type/internal-access compilation mistakes
were corrected with public APIs; no new friend access or reflection bypass exists.

## Complete declaration accounting, not a feature-completion percentage

| Classification | Baseline declaration shapes |
| --- | ---: |
| Exact normalized declared shapes | 869 |
| Shared-Core candidate review | 364 |
| Changed declarations at the same identity | 206 |
| Members not declared on matched types | 112 |
| Overload/parameter identity differences | 133 |
| Missing exported owner review | 56 |
| Framework-generated export review | 8 |
| **Total accounted** | **1,748** |

There are 1,609 target declarations and **879 raw missing-or-different baseline
entries**, with 740 additional-or-different target entries and no unresolved
metadata. Strict self-comparison passes. Candidate classification does not reduce
that raw count. Inherited external-framework members and custom attributes still
need fuller comparison; matching shape alone does not prove behavior.

Eight remaining owner families have no same-name Core candidate: the expression
chain visitor, lightweight observable, typed binding expression, both typed binding
classes, value-column base, column-selection interface and column-selection model.
These include Avalonia-specific extension APIs requiring explicit native/Core
compatibility decisions or implementation. Other differences include legitimate
Core relocations, native signatures, inherited APIs and generated exports. The
complete review names each declaration and preserves its original candidates.

## Performance acceptance still fails

[Paired run 35918998528](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35918998528),
job `107377807746`, artifact `10775689408`, completed both builds and all four
AB/BA host processes with valid frames. The unchanged **1.10 median time/allocation
ratio budget remains failed**:

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.48505 | 2.39870 | 4.945 |
| Vertical scroll | 1.02345 | 2.63000 | 2.570 |
| Distant diagonal scroll | 2.48950 | 11.12760 | 4.470 |
| Replace visible row | 2.51660 | 3.73135 | 1.483 |
| Resize visible column | 4.53075 | 6.28195 | 1.387 |
| Sort | 38.51140 | 71.31240 | 1.852 |

These are synchronous UI and verified layout-settlement measurements, not GPU
completion or frame rate. Sorting allocates less but remains slower. Comparing
separate hosted jobs is not a controlled revision experiment. No whole-grid
speedup is claimed for this audit continuation.

## Required before ready

Complete genuine public-contract implementations and tested Core/native mappings;
finish inherited/attribute and broader lifecycle review; meet unchanged native
performance budgets with hierarchy/variable-height/mixed-mutation workloads;
extend positive hardware keyboard/pointer/drag, Unicode/IME, screen-reader, DPI
and repeated multi-head runtime acceptance. Passing current fixtures is not closure
of these remaining gates. No release, merge or ready transition was performed.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/audit-uno-parity.py
python3 build/run-uno-native-suites.py --suite mutable-text-options --suite presentation-selection-hooks
dotnet test tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

All authored implementation is pushed. Local execution returned ClientError;
unknown unrelated local working-tree files could not be inspected. Performance
thresholds, trimming diagnostics, global rendering settings and Core ownership
rules remain unchanged. Later completed evidence supersedes this dated report.
