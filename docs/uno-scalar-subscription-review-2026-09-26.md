# Scalar-cell constructor lifetime and observer allocation

2026-09-26 UTC. Starting source `31f98578`; retained implementation `b30dfd65`.
[Nullable formatting correction](uno-nullable-formatting-review-2026-09-26.md) ·
[Current checklist](uno-current-work.md) · [Execution checkpoint](uno-ci-checkpoint-b30dfd65.json)

## The missing construction boundary

Raw `TextCell<T>` and `CheckBoxCell` previously assigned `_subscription` directly
from Subscribe. A synchronous callback could dispose the constructing cell before
Subscribe returned, after which the constructor stored the returned lease in the
already disposed cell. Typed constructors already handled that returning-lease
case, but both raw and typed constructors could escape still-live cell state when
Subscribe or an initial application notification threw without returning a lease.

This matters even without a public constructor-return value: the publisher owns
the observer and may retain it after the failed call. Later values or errors could
then continue reaching the partially constructed object. A custom publisher's
inaccessible registration cannot be forcibly unsubscribed by its subscriber.

The retained code records no lease until Subscribe actually returns. Synchronous
retirement makes subsequent callbacks inert immediately. A late-returned lease is
disposed once rather than stored. A throwing Subscribe retires the escaped cell and
rethrows the original exception. The publisher remains responsible for rollback
when it never returned a handle. Neither observable sources nor shared Core objects
are disposed merely because the cell retires.

Typed value/error generation checks and ordinary Value/Text/writer notification
order remain unchanged. Completion keeps the existing no-op behavior. Healthy
OnError followed by another source value retains existing recovery semantics.
The implementation does not add cross-thread synchronization or make a publisher
that violates its registration contract correct.

## Direct-owner raw observers

The old raw observer required two instance delegates plus the wrapper storing them.
A private observer now stores one cell owner and calls Receive/ErrorReceived directly.
TypedObserver and its BindingValue semantics remain in place. No new public member,
per-cell field, global cache, renderer path or Core state is introduced.

The first lifetime fix used a generic attachment helper. The retained version uses
concrete raw/typed overloads to avoid an extra generic method instantiation on the
constructor path. All four overloads preserve the same try/retire/rethrow and
late-lease rules. This is a code-path refinement, not a claim that every measured
latency improved; the complete controls below show otherwise.

## Same regressions against old and corrected code

The comparison workflow copies only the newly added test file into the baseline
test project. No old runtime/library source is edited. After the focused test run,
the copied file is removed and both product worktrees must pass git diff checks.
The baseline and retained candidate use the exact same 32 tests and test-file hash.

| Execution | Passed | Failed | Not executed |
| --- | ---: | ---: | ---: |
| Baseline `31f98578` plus new fixture | 20 | **12** | 0 |
| Retained `b30dfd65` plus same fixture | **32** | 0 | 0 |

The twelve failing baseline cases comprise raw late-returned-lease handling and
raw/typed failed-constructor retirement. Other cases protect completion, error
recovery, initial read versus writeback, borrowed publishers and recursive disposal.
No assertion was modified to turn a baseline failure into success.

Test-only reflection finds the constructing owner in either the old delegate
wrapper or new observer field. It is not a production dependency. The public
native/trimmed consumer uses no reflection: four raw/typed text/checkbox routes
exercise independent owners, synchronous initial values, writeback, captured stale
callbacks and exact release counts. A publicly observable throwing equality callback
reproduces constructor failure and demonstrates inert subsequent delivery.

Marker: `UNO_RUNTIME_SCALAR_SUBSCRIPTIONS_PASSED`. The new scenario is inside the
existing value-column-base suite, whose loaded-grid rendering/editing/sorting and
virtualization assertions remain unchanged. The focused new checks operate on
native cell models rather than another independently attached visual tree.

This continuation contributes 32 scalar tests, seven nullable-format tests and one
composite consumer scenario: **39 new Uno cases**, zero new paired-framework cases,
zero new registered native suites. Earlier interrupted numeric work is preserved,
not recounted as authored here.

## Controlled scalar comparison

[Retained run 36263496161](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263496161)
executes exact `31f9857801a890e8a760aaafc318d383daacc1ff` and
`b30dfd65a96783f3b3a98698aa8dbbb8020b617a` with one identical public harness
outside the product worktrees. ABBA order means baseline/candidate/candidate/baseline.
Each host warms twelve 4,096-operation batches and records twenty-five batches,
giving fifty samples per workload/revision. All four hosts, values, write counts,
lease counts and source checks passed. Raw samples, per-pass medians, p95, baseline
failures and candidate results remain in artifact `10912883960` (seventeen files).

Environment: .NET 10.0.12, X64 Ubuntu 24.04.5 LTS, workstation GC, inherited runtime
defaults with no tiered-compilation or ReadyToRun override. The caller-owned benchmark
publisher/lease is reused so these figures isolate scalar construction, synchronous
initial delivery, optional writeback and disposal. Formatting, native visual layout,
whole-grid rendering, startup and GPU completion are not measured here.

| Workload | Baseline ns/cell | Retained ns/cell | Time change | Bytes before | Bytes after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Raw text, read-only | 114.54 | 153.05 | **+33.62%** | 256 | 120 |
| Raw text, editable | 179.76 | 166.60 | -7.32% | 256 | 120 |
| Raw checkbox, read-only | 106.40 | 92.79 | -12.79% | 232 | 96 |
| Raw checkbox, editable | 161.66 | 147.29 | -8.89% | 232 | 96 |
| Typed text, read-only | 127.28 | 136.79 | **+7.47%** | 144 | 144 |
| Typed text, editable | 265.67 | 247.53 | -6.83% | 144 | 144 |
| Typed checkbox, read-only | 115.01 | 143.02 | **+24.35%** | 120 | 120 |
| Typed checkbox, editable | 223.62 | 221.80 | -0.81% | 120 | 120 |
| Constant text | 23.67 | 24.82 | **+4.85%** | 96 | 96 |
| Constant checkbox | 21.11 | 21.45 | **+1.62%** | 72 | 72 |

The allocation saving repeats in every measured pass: **136 bytes per raw cell**,
53.125% of this raw-text workload and 58.621% of this raw-checkbox workload. Typed
and constant controls allocate the same amount as their baseline. These are object
sizes measured on this runtime/architecture, not portable CLR guarantees or proof
that all grid paths use the raw observer API.

Timing is deliberately not summarized as a universal improvement. In particular,
raw read-only text worsens by 33.62% and typed read-only checkbox by 24.35%. Typed
editable text has a lower median but a higher p95 (273.22 to 278.74 ns). Baseline
raw read-only text has a much larger p95 than its median; every sample is retained.
The fix is retained for correctness and reproducible allocation reduction, with
these explicit latency tradeoffs. Results are diagnostics, not confidence intervals
or complete causal attribution; no result is dismissed as proven noise.

The earlier generic-helper experiment remains at
[run 36263106695](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263106695),
artifact `10913062888`. It also reproduced the same twelve baseline failures and
passed all 32 candidate cases, but typed read-only text was 50.85% slower, raw
read-only checkbox 14.96% slower and raw editable checkbox 12.26% slower. Its complete
ten-workload results remain preserved. It is a different source and runner execution,
not a rerun whose unfavorable samples were discarded. Absolute times from those two
runs must not be combined into a before/after claim for the overload change.

## Reproduction and acceptance

```bash
python3 build/compare-scalar-subscriptions.py \
  --baseline 31f9857801a890e8a760aaafc318d383daacc1ff \
  --candidate b30dfd65a96783f3b3a98698aa8dbbb8020b617a \
  --output artifacts/scalar-subscriptions-independent
```

Use a fresh output directory. The collector requires all 32 cases to execute, a
real baseline failure, all candidate cases passing, all ten measurement workloads,
correct checksums/writebacks, and clean product worktrees. It retains failure
reports rather than treating incomplete execution as success. Its green status
means collection completed, not Avalonia performance parity.

No public signature or API-audit mapping changed. Shared Core, native measurement,
rendering, preexisting tests and the independent 1.10 performance threshold are
unchanged. The remaining API adaptations, native layout/text and sorting costs,
hierarchy/variable-height workloads, physical input, IME and external accessibility
are still open. Current functional/platform/native-grid evidence is tracked separately
in the source-specific checkpoint; none is inferred from these microbenchmarks.
