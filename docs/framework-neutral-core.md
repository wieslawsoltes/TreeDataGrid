# Framework-neutral models

`TreeDataGrid.Core` contains the `TreeDataGridCore` namespaces and has no Avalonia dependency. The namespace intentionally differs from the package name: a top-level `TreeDataGrid` namespace would shadow the existing Avalonia control in consumers.

The existing `TreeDataGrid` package, assembly, namespaces, sources, columns, cell selection and control API remain available. Existing consumers can keep their source code. Binary compatibility is not promised; rebuild consumers. The legacy model implementation remains available during migration, with shared input handling. The opt-in adapter delegates to Core's row projection and selection; it does not build a second row source or selection engine.

## ViewModel

Reference only `TreeDataGrid.Core` and use these namespaces:

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

Flat and hierarchical sources own items, stable sorting, row index paths, selection and expansion. Column definitions hold visibility, ordering, requested sizes, optional presentation keys, comparisons and value accessors. There is no new grouping engine: consumers such as VirtualGrid continue to own grouping and project their existing grouped rows.

`HierarchicalExpanderColumn` accepts child, optional has-children and optional read/write expansion selectors. Bound expansion works before a view exists and responds to `INotifyPropertyChanged`. `Expand`, `Collapse`, recursive expansion and row selection also work without an Avalonia runtime.

Core notifications run on the caller's thread. Serialize model updates; when presenting a source, marshal its changes to the UI thread in the application. The legacy API retains its existing dispatcher behavior.

## View

Reference `TreeDataGrid` and change the binding from `Source` to `Model`:

```xml
<TreeDataGrid Model="{Binding People}" />
```

The control creates a typed adapter without reflection. It disposes presentation subscriptions on detach and recreates them on reattach, preserving the model and selection. The `Source` property continues to accept existing Avalonia sources.

For named templates or custom UI columns, the view can construct an adapter explicitly:

```csharp
var options = new TreeDataGridPresentationOptions<Person>();
options.Columns["person-card"] = column => new TemplateColumn<Person>(
    column.Header, personTemplate);
var presentation = new TreeDataGridSourceAdapter<Person>(viewModel.People, options);
grid.Source = presentation;
```

The ViewModel supplies a neutral `TemplateColumn<Person>("Person", "person-card")`; the view supplies the Avalonia template. Detach `grid.Source` and dispose the explicit adapter when that presentation ends. Disposing an adapter never disposes its model. Default `Model` binding resolves built-in text, checkbox and hierarchy presentations; custom presentation factories use the explicit adapter.

Core currently provides row selection. Consumers needing the existing cell/column selection APIs can retain the legacy source API until they choose to migrate. UI input remains in the Avalonia assembly, and both source paths share the same keyboard, pointer and search implementation.

## Boundaries and validation

`build/FrameworkNeutral.targets` rejects Avalonia references at build time. `build/check-neutral-dependencies.py` inspects the restored Core and Core.Tests package graphs and exercises the negative reference guard against a real UI DLL. Core tests run without Avalonia; the original UI suite and adapter tests cover compatibility, rendering, edits, selection, expansion and lifecycle.

```sh
dotnet test tests/TreeDataGrid.Core.Tests -c Release
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests -c Release
python3 build/check-neutral-dependencies.py
```

`VirtualizationBenchmarks.NeutralSource` compares legacy and neutral adapter rendering with the same workloads. `NeutralSourceBenchmarks` compares creation, sorting and expansion. See `benchmarks/README.md` for reproducible runs.

## Avalonia 12

This branch targets .NET 8 and Avalonia 12. The Core C# sources and public model API are identical to the v11 branch. The Avalonia adapter translates selection events to v12's `TreeDataGridSelectionChangedEventArgs` and supports automatic row-selection creation through `SelectionMode`. Existing declarative columns, `ItemsSource`, and legacy sources remain available. Core sources currently support row selection; selecting cell mode requires the legacy source API.

Validation: 105 Core tests, 462 UI tests (including attached control events and selection mode), 5 demo tests, solution build, package creation, and the dependency guard pass. Representative v12 benchmarks are included alongside the v11 results.
