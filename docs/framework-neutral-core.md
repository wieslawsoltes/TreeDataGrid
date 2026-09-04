# Framework-neutral models

`TreeDataGrid.Core` is the primary model API. It uses the `TreeDataGridCore` namespace and has no Avalonia dependency. The namespace differs from the package name so it does not shadow Avalonia's `TreeDataGrid` control.

The control consumes Core rows directly. Each view owns its column layout, cell bindings and input handling. Core models are not converted into legacy sources, rows, columns or selection models. The earlier unpublished `TreeDataGridSourceAdapter` API has been removed.

New Avalonia views should reference `TreeDataGrid.Avalonia`. The existing `TreeDataGrid` package, assembly, source and column APIs remain available for compatibility. Existing applications can continue to bind `Source`; the compatibility presentation connects those sources to the same renderer. Binary compatibility is broken, so rebuild consumers. Legacy sources retain their model behavior and share keyboard, pointer and search handling with Core. See [Avalonia package identity](avalonia-package.md).

## ViewModel

Reference only `TreeDataGrid.Core`:

```csharp
using TreeDataGridCore;
using TreeDataGridCore.Models;

public FlatTreeDataGridSource<Person> People { get; } = new(people)
{
    Columns =
    {
        new TextColumn<Person, string>("Name", x => x.Name, (x, value) => x.Name = value),
        new CheckBoxColumn<Person>("Enabled", x => x.Enabled, (x, value) => x.Enabled = value),
    }
};
```

Flat and hierarchical sources own items, sorting, row index paths, selection and expansion. Columns contain visibility, order, requested sizes, presentation keys, comparisons and accessors. VirtualGrid retains its existing grouping, sorting and keyed selection engine and supplies its projected rows to a Core flat source with TreeDataGrid selection disabled.

`ValueColumn<TModel, TValue>.FromDelegate` accepts an existing accessor without building or compiling an expression. Its default view binding observes `INotifyPropertyChanged` on the row model; expression-based columns also support observation through nested property paths. Generated consumers such as VirtualGrid can retain their compiled delegates.

`HierarchicalExpanderColumn` accepts child, optional has-children and read/write expansion selectors. Bound expansion observes nested property paths, reconnects when intermediate objects change, and releases those subscriptions when rows are removed or the source is disposed. Bound expansion, `Expand`, `Collapse`, recursive expansion and row selection work without an Avalonia runtime. Core notifications run on the caller's thread. Serialize model updates and marshal changes to the UI thread while a source is displayed.

## View

Reference `TreeDataGrid.Avalonia` and bind the Core source directly:

```xml
<TreeDataGrid Model="{Binding People}" />
```

No adapter is required. The control creates view state using typed visitors without reflection. Detachment suspends model subscriptions and preserves view-owned columns; reattachment synchronizes changes and resumes input. Replacing the model or presentation options disposes the old view state. It never disposes the model. Use the Core source's `RowSelection` in new consumers; `TreeDataGrid.Source` and `TreeDataGrid.RowSelection` remain the legacy API and are not populated by a Core model.

Named templates are registered in the view:

```csharp
using Avalonia.Controls.Presentation;

var options = new TreeDataGridPresentationOptions<Person>();
options.Columns["person-card"] = column => new Avalonia.Controls.Models.TreeDataGrid.TemplateColumn<Person>(
    column.Header, personTemplate);
grid.PresentationOptions = options;
grid.Model = viewModel.People;
```

The model supplies `new TreeDataGridCore.Models.TemplateColumn<Person>("Person", "person-card")`. Set view options before assigning a model that needs custom presentations. Built-in text, checkbox and hierarchy columns need no options.

Custom cell presentations implement `ICellColumn<TModel>`, which creates cells directly from Core rows and supplies UI layout behavior. The existing UI text, checkbox and template column classes implement this contract, so their cell implementations are shared. Requested widths, visibility and sorting belong to Core; actual measured widths belong to each view. Multiple views have separate cell/layout objects over the same models.

A custom renderer or overlay can explicitly own `TreeDataGridPresentation.Create(model, options)`. This is view state, not a data source or compatibility adapter. Dispose it after unrealizing its cells. Core row objects and collection-change arguments are preserved by reference, and disposing view state leaves the Core model usable.

## Compatibility

Normal legacy source construction and `Source` binding continue to work. Existing custom `IColumn<TModel>`, `IRow<TModel>` and `IExpanderCell` implementations remain supported on that path. Their neutral row contracts use default interface implementations without allocating row wrappers.

Low-level renderer customizations should use `ITreeDataGridRows` and `TreeDataGridCore.Models.IRow`: the control and row presenters now render this common row contract. `IExpanderCellPresentation` is the corresponding native expander-cell contract. Legacy interfaces remain available. For drag data, old sources continue to use `DragInfo.Source`; Core sources use `DragInfo.Model`.

Core currently provides row selection. Existing cell/column selection remains available through the legacy API.

## Complete Core sample

`samples/TreeDataGridCoreDemo` mirrors the full demo catalog while using `TreeDataGrid.Model` and Core sources/columns exclusively. It source-links the legacy demo's common country, file-system, Wikipedia, and drag/drop models, keeping Avalonia templates in `TreeDataGridPresentationOptions`. Its headless test verifies all eight grids avoid the legacy `Source` path and captures Countries and nested People renderings.

## Validation

`build/FrameworkNeutral.targets` rejects Avalonia references in Core. `build/check-neutral-dependencies.py` checks package graphs and exercises the negative reference guard against a real UI DLL. Tests cover legacy behavior, Core rendering/editing, direct row identity, notifications, templates, selection, expansion, accessibility, multiple views and disposal.

```sh
dotnet test tests/TreeDataGrid.Core.Tests -c Release
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests -c Release
python3 build/check-neutral-dependencies.py
```

`VirtualizationBenchmarks.NeutralSource` compares legacy and native Core rendering with the same workloads. `NeutralSourceBenchmarks` covers source/presentation creation, sorting and expansion. Historical split measurements remain in `benchmarks/MVVM_SPLIT_RESULTS.md`; the native API correction is measured separately.

Compiled expression delegates are weakly cached in the UI assembly. Mutable binding settings, link arrays, layout, and cell subscriptions remain separate for each view. Detach/reattach preserves view-owned column state while removing Core event subscriptions, so the source does not retain a detached control.

Review fixes also ensure hierarchical presentations dispose their owned custom inner columns, including when replacing a presentation key or disposing a hidden column. Regression coverage is in `ExpansionBindingTests` and `CorePresentationRegressionTests`.

Simple root expansion selectors retain direct model subscriptions; only nested selectors allocate a path subscription. Unbound hierarchy rows do not allocate a callback for expansion observation.
