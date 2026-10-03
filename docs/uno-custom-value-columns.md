# Custom native value columns

`Uno.Controls.Models.TreeDataGrid.ColumnBase<TModel, TValue>` restores the custom
value-column extension pattern while retaining the shared-Core architecture.
This guide describes the implemented native mapping, not binary compatibility
with Avalonia or complete parity of every built-in class hierarchy.

## Derive a column and use the existing binding engine

```csharp
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using CoreRows = TreeDataGridCore.Models;

public sealed class Person : INotifyPropertyChanged
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;
            _name = value;
            PropertyChanged?.Invoke(this, new(nameof(Name)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class PersonNameColumn : ColumnBase<Person, string>
{
    public PersonNameColumn()
        : base(
            "Name",
            person => person.Name,
            (person, value) => person.Name = value ?? string.Empty,
            new GridLength(220),
            new TextColumnOptions<Person>())
    {
    }

    public override ICell CreateCell(CoreRows.IRow<Person> row) =>
        new TextCell<string?>(
            CreateBindingExpression(row.Model),
            isReadOnly: false,
            options: (ITextCellOptions)Options);
}
```

The protected factory produces a separate `TypedBindingExpression` for each cell.
It observes the original model and nested notification owners. A cell owns its
subscription, not the model, source observable or descriptor. The cell-expression
root is weak between active observations; the source itself remains the caller's
responsibility. Disposal does not dispose a caller-provided subject or expression.

For a nullable checkbox, derive `ColumnBase<Person, bool?>` and return
`new CheckBoxCell(CreateBindingExpression(row.Model), false, true)` from the same
public `CreateCell` contract. The native checkbox control consumes that model's
three-state and writeback metadata; no bespoke control or observer adapter is
required in the application.

## Connect the view to an actual Core definition

The Core definition owns model semantics and sorting. The native column is a view
provided by the presentation factory; it does not introduce another Core source.
For a newly configured grid:

```csharp
var source = new TreeDataGridCore.FlatTreeDataGridSource<Person>(people);
var definition = new TreeDataGridCore.Models.TextColumn<Person, string>(
    "Name", person => person.Name, (person, value) => person.Name = value,
    width: new TreeDataGridCore.GridLength(220))
{
    PresentationKey = "Person.Name.Custom",
};
source.Columns.Add(definition);

var options = new Uno.Controls.Presentation.TreeDataGridPresentationOptions<Person>();
options.Columns.Add("Person.Name.Custom", _ => new PersonNameColumn());

grid.PresentationOptions = options;
grid.Model = source;
```

The application should detach `grid.Model` before disposing the source it owns.
A reusable view factory must return a fresh view instance for each presentation.
A view can implement `IDisposable` for additional view-owned resources; each cell's
subscription is already released by normal native cell retirement.

The custom column's `GetComparison` is its public extension contract. Register
model sorting policy on the Core definition when customizing actual grid sorting;
a presentation factory does not silently overwrite that caller-owned policy.

## Options and construction contracts

The typed `Options` property and the inherited `CellColumnBase<TModel>.Options`
view refer to one live sizing policy. Changes through either surface are visible
through the other. These options have no change event: query/measurement consumes
the current configuration, rather than introducing an automatic refresh timer.

Sort enablement and ascending/descending comparison delegates are captured at
column construction, matching the reference base. Repeated comparison queries
return the cached delegates. Null-model ordering, invalid sort directions and
custom comparers are covered by direct two-framework tests.

The constructor accepting `Func<TModel, TValue?>` and
`TypedBinding<TModel, TValue?>` deliberately keeps them distinct. A caller may
supply a different sorting/value selector and binding reader; neither is silently
replaced with the other. The descriptor remains mutable and caller-owned, while
an instantiated expression snapshots its read/write/link configuration.

`ColumnBase<TModel>` reuses `CellColumnBase<TModel>` for native measurement and
Pixel/Auto/Star layout. Existing built-in native facades keep their existing
`ValueCellColumn` hierarchy and bounded recycling path. Their inheritance is not
being declared identical to the legacy Avalonia combined model/view hierarchy.

## Typed cell constructor mapping

Native `TextCell<T>` and `CheckBoxCell` accept
`IObservable<Uno.Data.BindingValue<T>>`, with an optional separate
`IObserver<Uno.Data.BindingValue<T>>` writer. A descriptor expression implements
both and can be passed directly. This is the native analogue of the reference
Rx subject constructor without adding a new Rx dependency. Existing raw-value
observable constructors remain available.

Unset/DoNothing leave the last scalar value intact. A diagnostic without a value
preserves that scalar; a diagnostic with fallback publishes the fallback and
retains its exception. A subsequent normal value clears the diagnostic. Nested
publications, local assignments and source errors cannot restore an older
fallback diagnostic after a newer result. Native diagnostics extend the reference
cell's historically value-only observation; this is an explicit behavior contract.

Write rejection follows the supplied observer's policy. In particular, the
experimental expression's public subject `OnNext` retains its reference
refresh-on-rejection policy. The native dependency-property attachment's explicit
throwing write path is a separate contract; these constructors do not misrepresent
an arbitrary observer as transactional validation.

## Validation scope

`ValueColumnBaseParityTests` compares actual Avalonia/native column bases, not a
reimplementation of the reference. `TypedCellConstructorParityTests` compares
both actual text/checkbox constructors. `value-column-base` is a registered native
suite, also executed in the sequential published-consumer path, covering editing,
nested-owner replacement, nullable checkbox updates, row replacement, header/width
propagation, distant scrolling, Core sorting, bounded realization and cleanup.

The ordinary string identity format and immutable notification arguments avoid
per-query/per-notification allocation. Custom culture formatters, alignment and
format errors still use the ordinary formatting implementation. Focused warmed
allocation tests and the full native paired benchmark are separate acceptance
gates; the former do not certify full-grid timing or performance parity.
