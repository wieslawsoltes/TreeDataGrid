# Binding reassignment: instruction reuse, snapshot semantics and measured costs

2026-09-26 UTC. Baseline `c392b11d`; product `18323cb8`; tested checkpoint `081f62ac`.
[Current checklist](uno-current-work.md) · [Execution checkpoint](uno-ci-checkpoint-081f62ac.json)

## Prior work preserved, not recounted

The branch already contained recovered portable declarations and JIT accessor work
(`361dd48b`), generic presenter declarations and reduced member-path compilation
(`920e2f9e`), and specialized-cell declarations plus binding instruction snapshots
(`c392b11d`). The current continuation starts at that last commit. Those changes
are now covered by a current checkpoint, not claimed as newly authored here.

The previous descriptor cached a Core value-column instruction and an owned copy
of its owner-link array. Expressions retained independent roots, values, observers,
fallbacks and disposal. However, assigning the same Read or Write delegate discarded
the column instruction, and assigning the same Links array discarded its snapshot.
The next expression rebuilt identical instructions even though the immutable
instruction identities had not changed.

## Retained implementation

Only `Experimental/Data/TypedBinding.cs` changes in the runtime library. Read and
Write keep their cached instruction when the assigned delegate is reference-identical.
A distinct delegate invalidates it even when Delegate.Equals would return true.
Links retains the snapshot for a reference-identical array, but GetPlan still compares
each element against its owned snapshot. Reassigning a mutated array cannot bypass
validation. A different array keeps the previous immediate-invalidation behavior.

Every assignment still increments CellRevision, including same-value and repeated
null assignments. A live cell's descriptor-generation check is therefore not skipped
merely because instruction storage can be shared. The public mode, required writer,
required reader/link array, fallback and source rules are unchanged. Validation runs
before returning an expression; null elements fail, and an earlier expression remains
usable after a failed attempted snapshot. Repairing an array can reuse its unchanged
old snapshot without sharing the caller's mutable array.

No plan contains a current root or observation. Existing and newly created expressions
still own independent values, writers bound to their own roots, subscriptions and
cleanup. No global cache, additional field, copied Core source or native layout shortcut
was introduced. The cache identity policy is not thread-safety: concurrent descriptor
or link-array mutation is not made safe by these changes.

A typical redundant configuration path now reuses its instructions:

```csharp
var read = binding.Read;
var write = binding.Write;
var links = binding.Links;
binding.Read = read;
binding.Write = write;
binding.Links = links;
using var expression = binding.Instance(currentModel);
```

Avoiding unnecessary assignments at the call site is still preferable. This change
handles callers that repeat configuration; it does not require that pattern.

## Authored regression coverage

`TypedBindingReassignmentTests` adds eighteen cases. Six cover the three properties
through public Instance and the retained-cell factory, with revision advancement,
shared immutable instruction identities, independent writes and cleanup. Two reject
delegate equality as a substitute for reference identity. Additional cases cover
in-place array mutation, invalid-array repair, different arrays, uninitialized-cache
revision increments, five binding modes, and invalid reader/writer/link descriptors.
Reflection is confined to inspecting instruction identity outside timed measurements.
No assertion in a test predating this continuation changed.

The new public-consumer scenario is a partial of TypedBindingPlanRuntimeChecks and
runs inside the existing value-column-base suite. It performs 32 repeated assignment/
creation cycles over independent roots and actual native TextCell models, checks
writeback and observer counts, rejects null owner links, repairs the descriptor,
retains an older expression's writer after mutation, and verifies final cleanup.
Marker: `UNO_RUNTIME_BINDING_REASSIGNMENT_PASSED`.

This is one composite consumer scenario, not a new registered native suite or a new
paired-framework test. Its enclosing suite retains loaded native rendering, editing,
owner replacement, sorting and virtualization coverage; the new focused assertions
operate on native cell models rather than a separate attached visual tree.

## Controlled diagnostic and full retained results

[Run 36242946975](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36242946975)
executes the exact baseline and candidate using one identical public harness outside
both worktrees. Process order is baseline/candidate/candidate/baseline. Each workload
warms eight 4,096-operation batches and records fifteen batches per host, producing
thirty samples per workload/revision. All four hosts completed and all checksums,
subscription cleanup checks and source-dirtiness checks passed.

Environment: x64 Ubuntu 24.04.5, .NET 10.0.12, workstation GC. Tiered compilation and
ReadyToRun are disabled equally for both measured revisions. This avoids changing
those policies between hosts but is not default-runtime acceptance. Library and harness
fingerprints, all raw samples, p95 and per-pass medians remain in the twelve-file
artifact `10905764930`. It is a managed creation/observation diagnostic, not a native
control, layout, whole-grid sorting, startup or frame-rate measurement.

| Workload | Baseline ns/op | Candidate ns/op | Time change | Baseline bytes/op | Candidate bytes/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| Stable descriptor | 91.08 | 103.03 | **+13.12%** | 280 | 280 |
| Same reader | 114.09 | 86.89 | -23.84% | 488 | 280 |
| Same writer | 110.50 | 82.80 | -25.07% | 488 | 280 |
| Same links | 93.69 | 83.44 | -10.94% | 320 | 280 |
| All three unchanged | 129.69 | 89.48 | -31.01% | 528 | 280 |
| All unchanged, observing | 471.92 | 420.07 | -10.99% | 864 | 616 |
| All unchanged, native TextCell model | 534.48 | 476.73 | -10.81% | 1008 | 760 |
| Genuinely changed reader | 117.00 | 113.11 | -3.33% | 488 | 488 |
| Replaced link array | 163.48 | 164.17 | **+0.43%** | 360 | 360 |
| Mutated link array | 102.78 | 106.98 | **+4.09%** | 320 | 320 |
| Transient descriptor | 129.90 | 132.37 | **+1.91%** | 616 | 616 |
| Direct expression constructor | 103.50 | 106.87 | **+3.26%** | 504 | 504 |

All values are pooled medians. The unchanged three-input assignment path saves
248 bytes per measured creation, a 46.97% reduction for the unobserved-expression
workload. The same 248-byte reduction is 28.70% with observation and 24.60% with a
TextCell model. These sizes are measurements on this runtime/architecture, not
portable object-size guarantees. No allocation saving is attributed to the stable,
truly changed, transient or direct controls.

The allocation reduction is retained with explicit adverse timing observations:
the stable control is 13.12% slower, mutated links 4.09% slower, transient descriptors
1.91% slower and direct construction 3.26% slower. Replaced-link p95 also rises from
181.57 to 219.78 ns. Even unchanged control paths can have different process timing;
that observation does not establish the cause and is not dismissed as proven noise.
No statistical significance, universal speedup or complete causal attribution is
claimed. No sample or workload was discarded and no comparison retry was requested.

## Reproduction

```bash
python3 build/compare-binding-reassignment.py \
  --baseline c392b11d531b9255beddfa7d1faa907fe1dd943f \
  --candidate 081f62ac02065d4db7ee697a0ad69b2060cdacf1 \
  --output artifacts/reassignment-independent-reproduction
```

Use a new output directory for each execution. The collector records failure output
rather than interpreting an incomplete host as a successful result. The diagnostic
workflow's green status means collection succeeded, not framework parity.

## API and independent native acceptance

No exported declaration or audit algorithm changes in this continuation. The current
production reader reports 1,845 baseline / 1,888 target declarations, 1,055 exact matches,
790 missing-or-different and 833 additional-or-different. Shared Core contributes 590
identical dependency records, not 590 independently ported UI contracts. Dependencies
resolve, normalization collisions are zero and strict self-comparison has no differences.
The earlier reductions from 825 to 790 belong to recovered/previous commits.

[Independent native run 36242946887](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36242946887)
completes both framework builds and four hosts, but fails the unchanged 1.10 timing/
allocation budget. Timing ratios are 2.340 horizontal, 1.925 vertical, 3.697 diagonal,
1.451 visible-row replacement, 1.638 resize and 1.458 sort. Allocation ratios are
0.695, 1.957, 1.917, 2.668, 1.516 and 0.620 respectively. This separate run is not
combined with the managed ABBA diagnostic into a before/after native improvement.
It measures synchronous UI/layout and settlement, not GPU completion or frame rate.

Full port parity remains unproved. The unresolved API/native/Core/inheritance contracts,
vertical/layout costs, hierarchy and variable heights, earlier intermittent allocation
observation, physical input/drag, Unicode/IME and external accessibility remain open.
Consult the exact execution checkpoint for completed platform stages and evidence scope.
