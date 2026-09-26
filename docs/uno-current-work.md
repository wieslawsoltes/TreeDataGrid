# Current Uno completion checklist

Updated 2026-09-26 UTC. Tested implementation **1f81fb34**;
retained allocation change **0fefe457**.
**The declared API gap is reduced from 834 to 825 with individually reconciled
evidence. Binding creation uses less memory, but full API and performance parity
are not established; vertical timing regressed in the controlled comparison.**

[Presenter APIs and audit correction](uno-presenter-api-review-2026-09-26.md) ·
[Binding allocation and performance tradeoffs](uno-binding-allocation-review-2026-09-26.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-1f81fb34.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-1f81fb34.md)

## Source identity and ownership

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core owns sources, rows, hierarchy and selection. No copied
Core state, renderer change, merge or public release was introduced.

Starting head: `10f811fbac9d2735a83d2a03dac15febb3492cda`.
Audit correction: `81e94fcf123ffa9436a791adf5fcd8a70539f630`.
Presenter APIs: `73d4c0f34d17c7b85ab14033bdbf465abeca5a38`.
Both-lazy candidate: `7de9f507972b0f1ecbfb561b06e0004a630a1690`.
Retained collection-only candidate: `0fefe457352c7a5929d80c4202b68f451ee39c89`.
Tested checkpoint: `1f81fb342c351ab7ef9cf9193c1e2eb7a0841cad`.
Tested tree: `7c3e6a06d8c7ed76cdc46b88d673872e8b3c512f`.
CI merge: `ea2291669c41931b847b60255aad6f6aa6f8fe20`, with the identical tree
verified through the Git commit API. The checkpoint adds only the comparison
workflow to the retained runtime candidate. Final documentation follows completed
platform/browser execution and changes no product, tests, benchmarks or workflows.

## Nine declared differences resolved without waivers

Six old type differences came from sorting interface displays by raw namespace
before framework-name normalization. Avalonia and Uno names change their relative
position against System and Core names after mapping. The corrected reader keeps
the base class first and orders direct interfaces by normalized display, then raw
display. Every original name and interface is retained; generic arguments,
parameters, literals and signature modifiers remain ordered and unchanged.
This repairs the declared auditor's existing unordered-interface policy, not
arbitrary CLR interface equivalence. Schema 7 records the policy and sixteen
emitted-PE production-reader checks, including negative cases.

Three additional resolved declarations are newly implemented presenter
`TryGetTotalCount(out int)` methods. [Proof run 36221096429](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221096429),
artifact `10898917534`, reconciles every old record with its exact verified target:
**six ordering corrections + three implemented methods = nine resolved identities**.
No new missing identity appears. Core/native/inherited candidates are not waived.

| Scope | Baseline | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| All inputs | 1,845 | 1,853 | 1,020 | 825 | 833 |
| Identical shared Core | 590 | 590 | 590 | 0 | 0 |
| Independent UI | 1,255 | 1,263 | 430 | 825 | 833 |

Remaining categories: 208 changed same-identity declarations, 43 absent exported
types, 316 members of those types, 112 undeclared members on matched types and
146 overload/parameter differences. These sum to 825, not 825 absent features.
Scope accounting reconciles; dependencies resolve and collisions are zero. Strict
self-comparison is 1,853/1,853 declared and 15,819/15,819 supplemental exact, with
zero differences. Cross-framework parity still fails.

## Public presenter queries

Cells, column-header and row presenters each expose `TryGetTotalCount(out int)`
and `GetChildIndex(DependencyObject)`. Count captures Items and reads Count once,
without enumeration, cached totals or offscreen realization. Unset Items returns
false/zero; attached empty Items returns true/zero. Cells/headers report the full
visible-column projection, rows the full flattened visible-row count.

Child queries read the corresponding native container index. Wrong-type,
unrealized and retired containers return -1; parent membership is not validated,
as in the actual reference implementation. The three count signatures now match
exactly. The three DependencyObject parameters remain native adaptations of
Avalonia ILogical, not waived differences or implementation of its logical-tree
interface/ChildIndexChanged event.

`PresenterIndexRuntimeChecks` adds one loaded 200-row/12-column scenario inside
existing `cell-lifecycle`: absent/empty/custom Count, exception identity, actual
Core/container indices, distant reuse, hidden/reordered columns, removal/sorting,
4,096 queries without extra realization and complete retirement. Marker:
`UNO_RUNTIME_PRESENTER_INDEX_QUERIES_PASSED`.

## Allocation improvement with measured latency tradeoffs

The first candidate made both notification delegates lazy. Creation allocation
fell, but all six native timing medians worsened in its separate ABBA run. That
lazy property path was withdrawn. The original eager readonly property handler
and attachment path are restored; only collection observation is initialized on
first actual use. Handler identity, serialized callbacks, duplicate owners,
rollback and cleanup stay unchanged. No native measurement is skipped.

[Retained comparison 36221807680](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221807680)
uses exact baseline `73d4c0f3` and candidate `0fefe457`, the same creation harness
and pinned collector, ABBA revision order and alternating framework order.
All sixteen native hosts succeed; each Uno operation has 100 samples per revision,
with eight identical ordered layout/realization frame sequences, not pixel checks.

Managed plain/property-only cell creation allocates **376 to 312 bytes**, down
**17.02%** on the measured x64 runtime. Collection-only/dual models remain at 376.
That diagnostic measures public model/cell creation and disposal, not native
controls, startup or full-frame performance. Object sizes are not portable guarantees.

| Operation | Baseline ms | Retained ms | Time change | Baseline bytes | Retained bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 1.16680 | 1.16100 | -0.50% | 15,688 | 14,728 |
| Vertical scroll | 2.44180 | 2.58985 | **+6.06%** | 191,904 | 191,904 |
| Distant diagonal | 8.48515 | 8.09055 | -4.65% | 971,672 | 966,096 |
| Visible-row replacement | 3.26455 | 3.20220 | -1.91% | 101,248 | 101,248 |
| Visible-column resize | 6.61495 | 5.57815 | -15.67% | 132,528 | 132,528 |
| Sort | 59.46240 | 57.79870 | -2.80% | 1,038,616 | 1,038,616 |

Horizontal allocation falls 6.12%, diagonal 0.57%. Vertical timing increases 6.06%;
settlement medians increase 1.37% horizontal, 6.47% vertical and 0.32% replacement.
Although pooled p95 is lower for six operations, one candidate pass has a vertical
p95 of 11.6992 ms. The guide and artifact `10899284529` retain all raw/per-pass
results. The small allocation change is retained with that latency tradeoff, not
a universal speedup or statistically established causal claim. The first candidate
and artifact `10899485954` remain separate and reproducible.

## Completed validation

[Functional run 36221810127](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221810127)
passes all fifteen stages on unchanged input: **228 Core + 896 Uno + 536 Avalonia +
41 sample-state + 175 direct-framework = 1,876 .NET cases**, zero failed/skipped;
**65/65 native suites**; sequential native execution; both native sample builds
with zero warnings/errors; and five Activity Monitor sections plus lifetime checks.
Tooling passes 16 interface-ordering, 62 signature, 27 full-reader, 39 semantic,
57 normalization and 23 Python integrity checks. Report `10899810257`; source
snapshot `10899640442`.

Retained new coverage: **14 Uno cases, 16 audit-reader checks and one loaded native
scenario**. No new direct-framework case or registered suite. Narrowing updates
four newly authored allocation-policy expectations; the other ten new behavioral
cases and all preexisting assertions remain unchanged.

[Platform run 36221810072](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221810072)
passes all six jobs: Ubuntu/Windows/macOS builds/tests, Linux native/NuGet execution,
Windows App SDK build/publication, and actual published trimmed-browser consumers.
Browser job `108348484940` logs the presenter-query marker and four successful
routes: showcase, Activity Monitor and fifteen external pointer/keyboard stages at
each device scale 1 and 2. Chromium 143.0.7499.4, Playwright 1.57.0. Its completed
log identifies artifact `10900183456`. No separate local archive extraction is claimed.

Build, dependency, reference-pack, trimmed-binding and reproducibility workflows
also pass. No final-implementation retry was requested. The prior intermittent
allocation failure remains undiagnosed. Browser splash-screen warnings remain.
Windows publication is not native Windows runtime acceptance; automation does not
prove physical input, universal-browser, IME or external-screen-reader behavior.

## Independent budget and remaining acceptance

[Independent run 36221810071](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221810071)
completes both builds and all four hosts but fails the unchanged **1.10 timing/
allocation budget**. Time ratios Uno/Avalonia: horizontal 3.143, vertical 2.409,
diagonal 3.899, replacement 1.569, resize 0.897, sort 1.489. Resize timing is below
budget here but allocation is 1.489x; neither that operation nor the full gate
passes. Artifact `10899795200` retains raw results. Synchronous UI/layout/settlement
is not frame rate or GPU completion. Independent results are not mixed with ABBA
measurements to claim additional gains.

Remaining: all 825 declared differences with explicit native/Core/inheritance
proofs; native text/measurement, lifecycle and source-sort costs; vertical latency;
hierarchy/variable heights; the earlier intermittent allocation observation;
broader callbacks; physical input/drag, Unicode/IME, external accessibility and
cross-head lifecycle/scaling. Full parity remains unestablished.

Local tools returned ClientError. Execution ran in GitHub Actions. Source, completed
logs, returned artifact metadata and Git tree identities were inspected. No local
compilation, extraction, reconstructed source tree or recomputed archive hashes
are claimed in this continuation.
