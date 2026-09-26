# Declarative policy declarations and unformatted bound text

2026-09-26 UTC. Starting head `4f2dc4cc96043fb33dc157d76b96d63e8c62b366`.
Runtime implementation: `879e75ad5cf2da5f14b657d408a58e28b685be14`.
Corrected test fixture: `b38ca0f74446621fba43474c9759c130b7f852bb`.

[Current checklist](uno-current-work.md) · [Execution and measurement checkpoint](uno-raw-text-checkpoint-b38ca0f7.json)

## Six declaration owners, one existing policy store

The reference TreeDataGridColumn directly declares CanUserResize,
CanUserSortColumn, AllowTriStateSorting, CompareAscending, CompareDescending and
BeginEditGestures. Uno previously exposed those policies only through its
ColumnCreateOptions base. The native definition now declares all six properties
and forwards their accessors directly to that existing base storage.

This is a declared-API improvement, not six new behavioral features. No duplicate
policy fields, source models, column instances, event stores or subscriptions are
created. Configuration through a ColumnCreateOptions reference and configuration
through a TreeDataGridColumn reference still update the same values. Delegate
identity is preserved; no delegate-equality substitution or additional callback
wrapper is introduced by the new accessors.

CreateCommonOptions retains the reference's existing distinction: scalar policies
are captured in each returned options object, whereas an initially configured
comparison delegates to the definition's current comparison. Later assigning null
therefore retains its original failure behavior; an initially absent comparison
stays absent in the earlier snapshot. Native tri-state propagation remains its
existing view behavior. No attempt is made to redefine the reference's behavior
while adding the declarations.

The original reference source was inspected at the starting commit:
`src/Avalonia.Controls.TreeDataGrid/TreeDataGridColumn.cs`.

## Null-format consistency

The original scalar TextCell<T> uses parameterless ToString when no StringFormat
is configured. Uno's live TextColumn cell already followed that branch, but its
immutable Core-backed TextBoundCell used the presence of the options object alone
to select composite formatting. An options object with StringFormat set to null
therefore called string.Format with a null format instead of taking the raw-value
branch. CellColumn.FormatValue had the same inconsistency.

Both now test the format itself. Runtime null-format values follow the established
scalar contract; no public nullability annotation is changed. A null text value
remains null through ITextCell.Text, while the existing object-valued FormatValue
API returns an empty string for null. An explicitly configured display culture is
not consulted by the parameterless branch. Numeric ToString follows CurrentCulture
as the original scalar implementation does. Custom format strings and custom
providers continue through the existing composite-format helper.

Reference inspected: `src/Avalonia.Controls.TreeDataGrid/Models/TreeDataGrid/TextCell.cs`.

## Avoid the object-valued intermediate in sealed text models

LiveTextCell and TextBoundCell now use the existing protected TypedValue property
for the unformatted branch:

```csharp
get => _options is { StringFormat: { } format } options
    ? CellTextFormatting.Format(options.Culture, format, TypedValue)
    : TypedValue?.ToString();
```

Previously that final branch accessed public object-valued Value. For value types,
that introduced an argument box before ToString. The two affected text models are
sealed internal implementations. Their typed and object-valued properties read the
same CellBinding value; the public virtual Value and FormatValue APIs, custom
column dispatch, conversion, writes and observation machinery are unchanged.

TypedValue returns by value, not by reference. A mutable struct's ToString receives
a copy and must not modify the cached binding or caller's model. A reference value
retains its original reference identity. A ToString callback that updates its source
still returns the snapshot read for that invocation; the next invocation observes
the new value. Original exceptions propagate and later source values can recover.
No ToString result or culture is cached, and no string is retained across retargeting.

This remains parameterless ToString, not a substituted IFormattable call, custom
conversion, numeric-only formatter, or a new dynamic-code requirement. Allocation
benefits must be measured per runtime; no cross-platform object-size guarantee is
inferred from the implementation.

## Authored coverage

The continuation adds 49 .NET cases:

| Fixture | Cases | Scope |
| --- | ---: | --- |
| DeclaredDefinitionPolicyParityTests | 11 | Six declared owners/accessor shapes, nullable metadata, shared storage, callback identity, captured versus live policy, defaults |
| UnformattedBoundTextParityTests | 27 | Three native routes by nine value kinds against the original Avalonia scalar implementation, with changing CurrentCulture and a rejecting options provider |
| UnformattedBoundTextTests | 11 | Allocation versus an object-valued oracle, mutable struct copies, original errors/recovery, reentrant ToString, retarget/writeback/cleanup and live format switching |

The native consumer extends the existing value-column-base suite. It uses public
native/immutable cell models over the real Core rows, checks base/definition policy
storage, explicitly null formatting, independent owners, retained retargeting,
writeback, format switching and final observer cleanup. Its marker is
`UNO_RUNTIME_UNFORMATTED_TEXT_POLICIES_PASSED`. The enclosing existing suite retains
loaded native-grid rendering/editing/sorting/virtualization assertions. This focused
new scenario is not another independently attached visual tree or a new registered
native suite. No preexisting test assertion was changed.

### Corrected new-fixture assumption, retained initial failure

The first contract run `36266049705` compiled and executed 293 paired-framework
cases: 292 passed and the newly added defaults test failed. It incorrectly compared
GridLength.Auto.Value directly: the observed Avalonia payload was 0 and Uno's was 1.
Auto is a unit contract, not equality of those unused numeric payloads. The corrected
fixture asserts IsAuto independently on both native types and retains all pixel
minimum, gesture, callback and declared-signature checks.

Only that new fixture and the comparison workflow's explanatory step text changed
in `b38ca0f7`. Runtime sources, normalizer, harness and measurement policy did not.
The first comparison run `36266046811` is preserved as a failed collection, not a
successful baseline proof or a discarded timing sample. The corrected fixture is
run identically against both baseline and candidate. No GridLength mapping or
runtime layout workaround was added to make the test pass.

## Measurement and API reconciliation

The collector builds one identical external public harness against exact baseline
and candidate worktrees. It overlays only the three new regression fixtures onto
the old test projects, removes them after the negative-control runs, and verifies
that tracked product sources remain unchanged. A complete result requires all 49
cases to execute, all candidate cases to pass, and real baseline test failures.

The production API reader's source tree must be identical between revisions. It
reads both complete compiled declared inventories, with resolved dependencies,
additive scope counts and no normalization collisions. Reconciliation requires
exactly the six expected reference identities, unchanged reference records,
unchanged normalization policy, and every previous raw target record unchanged.
Additional unmatched declarations, removed declarations or changed old records fail
collection. The supplemental audit and canonical native-target audit remain
separate evidence; no inherited or Core relocation candidate is automatically
waived.

Ten measured workloads cover raw live/Core integer display, decimal, present and
absent nullable values, strings, object-valued data, formatted paths and the scalar
reference-style control. All measured routes work on both revisions; the previously
throwing immutable-null-format case is tested as a regression rather than treated
as a measurable baseline. Construction occurs outside measurement. Each host warms
twelve batches and records twenty-five batches of 8,192 queries. Process order is
baseline/candidate/candidate/baseline, giving fifty samples per workload/revision.

Runtime settings are inherited without a tiering or ReadyToRun override. Raw
samples, checksums, exact strings, library/harness/test fingerprints, per-pass
medians and p95 are retained. Results concern warm cell-model display queries, not
construction, startup, native visual layout, full-grid sorting, GPU completion or
frame rate. Pooled diagnostics are not confidence intervals or complete causal
attribution. Adverse controls must remain visible in the checkpoint.

## Reproduce

```bash
python3 build/compare-unformatted-text.py \
  --baseline 4f2dc4cc96043fb33dc157d76b96d63e8c62b366 \
  --candidate b38ca0f74446621fba43474c9759c130b7f852bb \
  --output artifacts/unformatted-text-independent
```

Use a fresh output directory. The collector emits failure evidence rather than
interpreting an incomplete test run or benchmark as success. Canonical Linux
validation remains `python3 build/validate-uno-linux.py` under its existing native
runtime dependencies. The standard platform, trimming and 1.10 native performance
workflows are unchanged.

All implementation changes are confined to three native runtime files:
TreeDataGridColumn.cs, Models/TreeDataGrid/TextColumn.cs and
Presentation/CellColumn.cs. Shared Core, original Avalonia source, layout/rendering
algorithms, existing tests and performance thresholds are unchanged. Remaining
native/Core/type/inheritance API adaptations and native layout, hierarchy, physical
input, IME and external accessibility acceptance are not proved by this patch.
