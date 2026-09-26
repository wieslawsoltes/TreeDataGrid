# Opt-in tri-state header sorting

This Uno view feature makes the previously inert `AllowTriStateSorting` option
functional. It does not replace the shared Core sorting engine. The default is
still `false`: unsorted → ascending → descending → ascending. With the flag set,
header activation cycles unsorted → ascending → descending → unsorted; the next
activation starts ascending again.

## Configuration

The existing fluent API configures the view policy while leaving the source a
real `TreeDataGridCore.FlatTreeDataGridSource<TModel>`:

```csharp
using TreeDataGridCore;
using Uno.Controls;

var source = new FlatTreeDataGridSource<Person>(people)
    .WithTextColumn("Name", person => person.Name,
        options => options.AllowTriStateSorting = true);
grid.Model = source;
```

The same option works on checkbox and template fluent builders and
`WithHierarchicalExpanderTextColumn`. A native typed view column can instead use
`Uno.Controls.Models.TreeDataGrid.TextColumnOptions<Person>` (or checkbox/template
options) with `AllowTriStateSorting = true`. That options object is read at header
activation, so changing its flag affects the next descending transition without
reconstructing columns or raising a property notification.

Declarative text/checkbox/template definitions already inherit the option:

```xml
<tdg:TreeDataGridTextColumn Header="Name"
                          Binding="{Binding Name}"
                          AllowTriStateSorting="True" />
```

Here `tdg` denotes `using:Uno.Controls`. For a declarative hierarchical expander,
configure the **inner column**, whose sort/resize policies the expander forwards.
Fluent creation and declarative common-option materialization capture scalar
configuration. Changing a creation descriptor after it has produced its column
is not the same as changing the live native options object.

`CellColumn.AllowTriStateSorting` is virtual for custom native columns. Custom
columns derived from the compatible `ColumnBase<TModel>` or legacy
`CellColumnBase<TModel>` use their options through the existing adapter. The typed
adapter also forwards `CanUserSortColumn` so a native permission veto applies to
all three transitions. Arbitrary interface-only legacy implementations without
these options retain the default two-state policy.

## Core ownership and observable behavior

The third activation calls `TreeDataGridPresentation.ClearSort()`, which delegates
to the actual Core source's existing `ClearSort()`. It does not construct another
source, copy an initial row order, change comparers, or create a second selection
model. Reset therefore uses the **current collection order**, including collection
moves and insertions made while sorted. Core clears the sort indicators and emits
its normal sorting notification. An already-unsorted source remains a no-op.

For hierarchical sources, the same Core operation restores root and child source
order while preserving expanded row objects and selected model identities. Multiple
active views of one source observe the same shared sort state; the decision to
use a third header state is view policy, not a separate per-view sort engine.

An application can explicitly call `presentation.ClearSort()` without a header
interaction. A disposed presentation rejects that new API. Direct source sorting
and reset remain explicit model operations; native `CanUserSortColumns` and
`CanUserSortColumn` govern user header activation, not programmatic Core calls.

## Callback and lifetime boundaries

The header captures its realization, activation request, owner, presentation,
column collection and current direction. Permission getters, the virtual cycle
policy getter, collection count and indexed identity are callback boundaries.
The transaction is revalidated after each; permission is read again immediately
before dispatch. Retired, disabled, resizing, hidden or foreign header instances
must not clear a borrowed source or the new source replacing it.

A newer source sort performed by a cycle-policy callback supersedes the old
transition when the header direction changes. A nested header activation has its
own request generation even if the same container/model survives. Once Core is
called, the header does not manually publish a glyph or overwrite newer callback
state. Exceptions are not swallowed and Core's existing sort failure behavior is
not presented as rollback semantics.

## Tests and acceptance scope

`TriStateSortingTests` contains 21 new cases covering the transition table, native
and copied options, fluent/declarative propagation, custom adapters, two-view
notifications, current collection order, hierarchy/selection and disposed access.

`TriStateSortingRuntimeChecks` adds ten cases to the existing
`builtin-column-comparison` runtime suite. It activates **loaded native header
controls** through their public automation Invoke provider, observes the delivered
Click, and checks source order, glyphs and realized Core-model identity. It also
checks policy changes, permission revocation, callback sorting, source replacement,
reordered/hidden columns and hierarchy. The existing registry remains 65 suites.
The marker is `UNO_RUNTIME_TRISTATE_SORTING_PASSED`.

The existing browser driver adds descending, reset and restart to its header-click
protocol. All 15 stages still use browser pointer/keyboard input, not scripted
.NET sorting or model mutation. The new marker is
`UNO_BROWSER_TRISTATE_SORTING_PASSED`. Driver-level checks alone are not browser
execution evidence; inspect the published-consumer results at both device scales.

This is an opt-in functional extension, not a claim that the original Avalonia
reference already implemented the third-state interaction. No Core implementation,
renderer setting, preexisting unit assertion, trimming gate or 1.10 performance
budget is relaxed. Full API, performance, physical-input, IME and cross-platform
accessibility parity remain separate acceptance work. Execution results and exact
commit identities belong in the corresponding CI checkpoint, not inferred from
this source description.
