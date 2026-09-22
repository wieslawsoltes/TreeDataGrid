# Uno port on the shared Core

## Scope and authoritative checkpoint

PR #26 replaces the source-linked architecture of PR #12 using the actual shared
Core assembly. Baseline master is `3ca47316d724e5e040ab0281a880e8df999b25fc`
(stable v12.0.0.7); the working branch is `codex/uno-core-port`. Changes are committed
directly to the existing draft PR, not to master. No release or merge is implied.

The latest status, exact tested revisions and CI evidence are in the
[current completion checklist](uno-current-work.md) and
[2026-09-22 geometry/cells/API report](uno-geometry-observable-validation-2026-09-22.md).
These supersede older source-only/UNRUN notes. Historical reports remain in the
repository as dated evidence, not descriptions of the current head.

**Complete API, functional and performance parity is not yet certified.**
The `5ed67958` product checkpoint passes 1,047 unit cases, all 33 independently
hosted native suites, sequential showcase, both native sample builds and Activity
Monitor. The native measurement-recovery failure in earlier reports is resolved.
The cross-framework metadata inventory and paired performance budget still expose
remaining acceptance work; a green functional report does not override them.

## Architecture

- `TreeDataGrid.Core` owns source objects, rows, sorting, expansion and row/cell
  selection. Uno and Avalonia use those objects directly, without a copied model
  layer or an Avalonia compatibility implementation in the Uno library.
- `TreeDataGrid.Controls.Uno` owns native binding subscriptions, declarative source
  construction, view configuration, column/row geometry, template selection,
  parent-retained recycling, editing, input and native accessibility.
- The grid accepts Core sources through Model and exposes its native Source/view
  contracts. Presentation options can register templates/custom columns by the
  Core presentation key. Public lifecycle and custom-factory contracts are tested.
- Samples reuse neutral data/models where appropriate. The showcase includes
  Countries, editable People, Templates, variable-height Countries, Wikipedia,
  Files tree/flat and Find Country. Activity Monitor exercises five metric sections.

## Required parity and evidence

| Area | Required behavior | Current evidence / remaining boundary |
| --- | --- | --- |
| Core identity | Actual shared source/row/selection objects | Core tests and native identity assertions pass |
| Binding | Nested/null owners, aliases, computed values, writeback, trim contracts | Unit/native suites and real trimmed consumer pass; broad native form equivalence remains review work |
| Lifetime | Source/unload/factory changes retire observers, values and pools | Reentrancy/cleanup suites pass |
| Recycling | Retained parent/template identity, bounded pools, no redundant row-owned child hide/show | Native replacement/sort/scroll/wide-grid suites pass |
| Row/column geometry | Uniform/sparse height, Auto/fixed/star width, mutations, exact boundaries | Unit/native sizing and allocation invariants pass |
| Bring-into-view | Grid and standalone variable-height targets and source supersession | Registered native suites pass |
| Selection and editing | Row/cell selection, cancellation, buffered edits, retry, current mappings | Programmatic/native fixtures pass; physical event transport remains separate |
| Declarative/public API | Native XAML/source extensions, custom cells/rows/presenters, events | Tested contracts pass; classified compiled declaration differences remain |
| Accessibility | Current peer roles, values, selection, toggles, expansion, stale providers | Native peer assertions pass; real screen-reader/multi-head verification remains |
| Platform heads | Desktop Skia, browser and Windows App SDK | Per-head build/package status must be read for the exact revision; publishing alone is not runtime verification |
| Performance | Same-workload allocation/timing comparable with Avalonia | Controlled paired measurements exist; 1.10 median ratio budget still fails |
| Final acceptance | Complete contract review, full functionality and performance evidence | PR remains draft; no 100% claim |

The old PR #12 (`9a5737226b5c26617da362e28ee3337812b88707`) remains a reference
for platform behavior, not the source/model architecture. Remaining public export
identities must be reviewed against intentional Core relocation and native framework
contracts; missing namespace matches must neither be silently accepted nor filled
with duplicated model/selection state. The audit preserves every raw difference.
