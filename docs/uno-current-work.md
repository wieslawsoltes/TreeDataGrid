# Current Uno completion checklist

This file is the current work queue. The long parity/API ledgers retain historical
checkpoints; their superseded TODOs must not be treated as newly discovered work.
Nothing in this checklist marks the port complete or substitutes source inspection
for runtime evidence.

## Authoritative state

- Working tree: `codex/uno-core-port`, committed checkpoint `46d867c1` plus the
  uncommitted implementation. No new implementation has been built or run.
- PR #26: OPEN/DRAFT, remote head `62f074b6`. Remote master was re-read during this
  checkpoint and remains `3ca47316`; no newer base currently needs rebasing.
- Package: `TreeDataGrid.Controls.Uno`, referencing the actual shared Core project.
- Implementation-first ordering remains in effect; checks authored below are UNRUN.

## Completed source-integration areas, pending validation

The source now contains Core-backed declarative/fluent sources and UI columns,
row/cell selection, editing, hierarchy, drag/drop, automation, source/column/cell
ownership, the full showcase and Activity Monitor, and desktop/browser/Windows
head definitions. Rows, cells and headers use the common presenter bases with real
override dispatch. They retain bounded parented pools and synchronous content
rebind; no per-recycle dispatcher closure was added. Public presentation members,
selection-interaction input/visual dispatch, and grid collection/presentation/
scroll dependency properties are now connected. Template-part getters now return
null before application/when absent, matching the reference rather than throwing.
Loaded-sample callers explicitly assert their existing template-ready assumptions;
the source fixture checks the unapplied state. Replacement collections are
published before the previous view is disposed, so disposal callbacks do not see
public Rows pointing at a disposed facade; a native custom-presentation case is
authored for that ordering.

The current theme comparison covers both reference dictionaries and the entire
native Generic.xaml: semantic brushes, sort/expansion paths, header states and
resizer, row selection, text/checkbox/template/expander controls, editor/display
hosts, validation/current states, and native Light/Dark/HighContrast dictionaries.
This pass corrected overlay-only row/cell borders, default string-header trimming,
the header's bypass of presentation sorting, and missing grid template properties.
Border thickness now participates in measure/arrange; row extent includes its
container chrome. Named reference borders and the public indentation converter
are available. Native state groups and retained editor/display hosts replace
Avalonia selectors/template swapping; visual and performance parity are unproven.

## Remaining implementation review — finite targets

1. **Advanced declarative binding boundary:** finish the comparison of attached
   properties, outer namescopes, relative sources and compiled bindings against the
   reference detached probe. Ordinary/nested/indexer/generated-metadata writeback
   and local name-subject ownership are implemented. Distinguish native binding/
   compiler differences from missing product behavior; do not keep a generic
   “all advanced bindings” TODO after this comparison.
2. **Shared lifecycle review:** review the newly integrated presenter ownership,
   generation retirement and source/factory/template-property callbacks together.
   Address concrete source findings, then use the authored failure/reentrancy
   fixtures during validation rather than indefinitely repeating source-only review.
3. **Committed text on Skia:** CharacterReceived is unsupported in the inspected
   framework source and its native Unicode key is internal. The prefix/cycling
   implementation and supported-head dispatch exist. The pending choice between
   an explicit limitation and a separate Uno framework fix is unresolved. Do not
   invent Latin-key mapping or claim IME/international input parity. No Uno
   framework checkout is authorized for modification by this checklist.

Native custom routed-event registration and XAML compiled-binding forms differ
between the frameworks. These are explicit mapping questions, not permission to
silently drop required behavior or add a copied model layer.

## Validation and delivery gates (not started for the dirty implementation)

1. Build the full Uno solution, all configured sample heads and packages; fix errors.
2. Run Core, Uno and sample unit suites plus every registered native runtime suite.
   New gates include generic/built-in presenters, presentation contracts, custom
   selection interaction, presentation DP publication/reentrancy, border layout,
   header trimming and indentation conversion.
3. Run every showcase and Activity Monitor scenario, including real pointer/key
   input, drag/drop, editing, sorting, resize and source replacement. Check native
   accessibility, light/dark/high contrast, scale and RTL. Injected/programmatic
   fixture calls are not evidence for OS event delivery.
4. Validate independent package consumers, package identities/dependencies and
   matching symbols on the applicable heads.
5. Measure timing and allocation against the relevant prior Uno/Avalonia baselines,
   including template recycling and parent/template identity retention.
6. Review the complete diff and report findings locally; implement findings and
   rerun affected checks. Re-read remote master, update the branch as needed, push
   PR #26 and inspect CI before declaring it ready. No merge/release is implied.
