# Uno and Avalonia TreeDataGrid Code Sharing Plan

## Objective

Keep TreeDataGrid behavior implemented once wherever possible, with Avalonia as the source-of-truth for platform-neutral logic and Uno retaining only the platform-adapter boundary.

## Implemented in This Branch

### Phase 1: Shared core consolidation

Status: **Done**

Completed work:

1. Added `src/TreeDataGrid.Uno/TreeDataGrid.Uno.LinkedAvalonia.props` to source-link the reusable Avalonia core into the Uno build.
2. Deleted the duplicated Uno copies for the newly linked files.
3. Added the small compatibility shim `src/TreeDataGrid.Uno/Compatibility/AvaloniaCollections.cs` so linked Avalonia code compiles unchanged.
4. Updated Uno selection wiring to work with the shared selection contracts instead of keeping a forked contract surface.
5. Fixed `HierarchicalTreeDataGridSource<TModel>` in the Avalonia source so both platforms now consume the same filtered lookup behavior.

### Phase 1 acceptance criteria

Status: **Met**

- Shared core builds in Avalonia.
- Shared core builds in Uno through linked sources.
- Uno parity tests pass against the linked implementation.
- Duplicate local Uno source copies for the linked set are removed.

## Current Target Architecture

### Shared authoritative code in Avalonia

This should remain implemented once in `src/Avalonia.Controls.TreeDataGrid` and be linked into Uno:

- data source core
- row/column model core
- most selection infrastructure
- helper utilities
- lightweight observable infrastructure

### Uno-owned adapter boundary

This remains local to `src/TreeDataGrid.Uno`:

- control shell and dependency property surface
- WinUI/Uno binding accessors
- Uno XAML column declarations and source extensions
- presenter, automation, theme, and visual tree code
- compatibility types required to compile Avalonia concepts on Uno

## Next Reuse Steps

### Phase 2: Shrink the Uno-only binding and declarative column layer

Status: **Done**

Completed in this pass:

1. Moved simple member-path parsing, resolution, read/write, link creation, and source conversion mechanics into the shared Avalonia-owned `TreeDataGridMemberPath` helper.
2. Updated the Uno binding accessor to become a thin WinUI `Binding` adapter over that shared helper.
3. Updated Avalonia reflection-binding path handling to reuse the same shared member-path helper.
4. Moved repeated column option mapping into the shared Avalonia-owned `TreeDataGridColumnOptionsFactory` helper.
5. Updated both Avalonia and Uno `TreeDataGridColumn` and `TreeDataGridSourceExtensions` to use the shared option-mapping helper instead of repeating the same wiring locally.
6. Moved declarative-source construction and selection-mode application into the shared Avalonia-owned `TreeDataGridDeclarativeHelper`.
7. Updated both Avalonia and Uno `TreeDataGrid.V12.cs` files to use that shared helper instead of keeping duplicated generation/selection code.
8. Moved the reusable reflection/expander/row-header model files back into the Avalonia-owned linked set so Uno no longer keeps forked copies of `ReflectionTextColumn`, `ReflectionCheckBoxColumn`, `ObjectHierarchicalExpanderColumn`, `FilteredExpanderColumn`, and `TreeDataGridRowHeaderColumnInternal`.

Expected result:

- less drift in XAML column parity work
- fewer Uno-only bugs in binding and setter inference

### Phase 3: Extract more shared behavior from the control shell

Status: **Done**

Completed in this pass:

1. Moved shared control-shell logic into the Avalonia-owned `TreeDataGridControlLogic` helper.
2. Unified selection-cancel dispatch, row-model lookup, sort-direction toggling, allowed row-drag effect calculation, and auto-drop validation/drop-position policy across Avalonia and Uno.
3. Updated both `TreeDataGrid.cs` implementations to delegate those non-visual decisions to the shared helper instead of duplicating them locally.
4. Linked the Avalonia `TreeDataGridCellSelectionModel` into Uno by adding small control/presenter compatibility overloads instead of keeping a forked Uno copy.
5. Split `TreeDataGridRowSelectionModel<TModel>` into a shared non-visual interaction/core partial and small Avalonia/Uno platform partials for focus, visibility, and modifier handling.

Expected result:

- a thinner platform-specific control shell
- less duplication when parity changes touch non-visual logic

### Phase 4: Evaluate a formal shared project only if it buys something concrete

Status: **Analyzed - not recommended at the current boundary**

Completed in this pass:

1. Reviewed the current two-package layout: `TreeDataGrid` remains a packable `net8.0` assembly, and `TreeDataGrid.Uno` remains a packable `net9.0` assembly that imports the linked Avalonia source set.
2. Measured the current shared boundary and confirmed that the linked-source model already centralizes **96** shared files.
3. Verified that the shared boundary is not assembly-neutral enough to justify extraction today:
   - **64** shared files define public types, so a new core assembly would require public type relocation or type forwarding.
   - **32** shared files define internal types or rely on internal/protected-internal access, so a new core assembly would require new `InternalsVisibleTo` wiring or surface widening.
   - **41** shared files reference Avalonia-shaped namespaces directly, and Uno currently satisfies those through its local compatibility layer.
4. Closed the phase with a decision not to introduce `TreeDataGrid.Core` yet, because it would add packaging and binary-compatibility churn without increasing actual source reuse.

Reopen this phase only if one of these becomes true:

1. The linked shared set needs independent packaging/versioning.
2. Build tooling or IDE behavior becomes materially worse because of linked compilation.
3. The remaining shared code grows enough that a formal `TreeDataGrid.Core` assembly would simplify ownership more than it complicates packaging.

## Maintenance Rules

### Rule 1: no reintroducing copied core files into Uno

If a file is linked from Avalonia into Uno, fix it in the Avalonia source unless the problem is truly platform-specific.

### Rule 2: platform differences must stay at the edge

When Uno needs custom behavior, prefer:

1. a compatibility shim
2. a small local adapter
3. a partial hook

Avoid cloning the full shared class unless there is no smaller option.

### Rule 3: parity fixes should land in the shared source first

The filtered root-lookup fix in `HierarchicalTreeDataGridSource<TModel>` is the model to follow:

- fix the shared file in Avalonia
- let Uno consume the fix automatically through the link

## Validation Matrix

Run these checks whenever the shared boundary changes:

1. `dotnet build src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj`
2. `dotnet build src/TreeDataGrid.Uno/TreeDataGrid.Uno.csproj`
3. `dotnet build samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -p:TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop`
4. `dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj`

If the local machine has the required runtime, also run:

5. `dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj`

## Success Criteria

This plan is successful when:

1. Shared behavior changes are usually one-file edits in Avalonia, not parallel Avalonia and Uno edits.
2. Uno-specific code is obviously platform-specific when reviewed.
3. New parity work lands mostly in the shared layer instead of extending the fork boundary.
4. The remaining Uno-only surface becomes smaller over time, not larger.
