# TreeDataGrid Core API demo

This companion application mirrors the complete legacy demo catalog while constructing every data source and column with **TreeDataGridCore**.

- Every grid binds **TreeDataGrid.Model**; none assigns the legacy **TreeDataGrid.Source**.
- Countries, file-system, Wikipedia, and drag/drop model/data files are linked from ../TreeDataGridDemo, so both apps exercise the same domain objects rather than copied fixtures.
- Countries covers flat rows, editing, sorting, filtering, selection, collection mutation, and row drag/drop.
- Find Displayed Row and BringIntoView exercise row/model identity and virtualized non-uniform rows.
- Files covers flat and hierarchical sources, bound expansion, custom presentation keys, check boxes, sorting, and live file-system collections.
- Wikipedia covers asynchronous, virtualized data with a view-owned image template and deterministic offline fallback.
- Drag/Drop covers Core hierarchical moves and per-model drag/drop policy.
- People deliberately binds expansion through person.Expansion.IsExpanded, exercising nested property-path observation without an Avalonia binding in Core.
- Template Column Reuse keeps only stable presentation keys in Core and registers reusable Avalonia templates through **TreeDataGridPresentationOptions**.

TreeDataGridCoreDemo.Tests opens the real window under the Skia headless backend, asserts that all eight grids have a Core Model and no legacy Source, exercises nested expansion, and captures deterministic Countries and People screenshots.
