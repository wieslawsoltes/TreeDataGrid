# Hierarchy ownership and typed native columns

Date: 2026-09-23 UTC. PR #26, `codex/uno-core-port`.
This report describes completed implementation, not complete API or performance parity.
Exact final execution is recorded separately in `uno-ci-checkpoint-9a57ca17.json`.

## Changes and ownership

Starting commit: `0ccd3c9e4192a9febc109d3f8ba6adb633cdf69c`.
Final product: `9a57ca17a34d10f86234625709b6dba9065fc2b6`.
Product tree: `dc6d77269bd7644db5d16aaf679ec3a0a83df8dc`.

Seven commits implement the changes and their tests: `e286631e`, `5578b823`,
`60f59a5b`, `613e4860`, `0204448c`, `e47bfbfa`, and `9a57ca17`.
The actual TreeDataGrid.Core assembly remains shared with Avalonia. No Core
source, row, selection or hierarchy implementation was copied into the view.
No dependency, global renderer setting, trimming diagnostic or acceptance
threshold was changed. The PR remains draft, unmerged and unreleased.

Local execution returned ClientError. The changes were committed through the
GitHub connection and executed by unchanged CI checkouts. All authored changes
are pushed; unknown unrelated local working-tree files could not be inspected.

## Exception-safe hierarchy subscriptions

The old generic ExpanderCellValue disposed its subscriptions sequentially, then
its owned inner cell. A throwing subscription removal could prevent the remaining
removals and skip inner disposal. It also reread the row's Model during cleanup,
which could return a different object or throw instead of removing the handler
from the model actually observed.

The extracted `Presentation/ExpanderCellValue.cs` now captures its original model
once, tracks installed observations and clears ownership before cleanup callbacks.
It independently attempts expression binding, native binding, row, captured model,
inner observation, child collection and inner-value cleanup. A single failure
preserves identity and dispatch; multiple failures remain ordered. The borrowed
Core row and collection are not disposed. The public Row/Inner access contracts
are retained; no promise is made that a deliberately retained retired wrapper
contains no references to its publicly exposed borrowed row.

Initialization and child-subscription transactions defer physical unsubscription
until application accessors return. This matters when an add accessor first
calls Dispose and only then actually attaches its handler. Disposal still marks
the value retired immediately, preventing further writes or callback publication.
Cleanup then observes and removes the completed attachment. Repeated disposal
cannot double-dispose the owned inner value.

Child-source updates are serialized and revision-checked around getters and old
collection detachment. A nested newer model change wins; the returning older
collection is not attached or published. Notifications already captured by a
multicast event do not revive disposed values. Explicit HasChildren bindings
continue to avoid evaluating the child getter: lazy expansion remains lazy.
Native HasChildren updates also validate a revision after application selectors.

An arbitrary custom event remover can throw before performing its own removal.
The implementation attempts every owned cleanup stage and reports such failures;
it cannot guarantee a dishonest or permanently throwing external accessor actually
unsubscribes. Tests verify the library's cleanup order and accessors that remove
then throw, without silently claiming otherwise.

## Factory and metadata callback boundaries

The expander column now includes inner configuration in its guarded ownership
transaction. If the inner factory or configuration retires the view column,
returning values are rejected and released. If configuration fails, the owned
inner value is disposed even if that cleanup also throws; both failures survive.
Ownership transfers to the generic expander constructor before metadata and
subscription installation. A constructor failure disposes the inner exactly once
instead of relying on two competing catch blocks.

CanEdit, CanWrite and ShowExpander now recheck retirement after application-defined
getters return. A getter that disposes the expander cannot return a stale true
permission or visible state. Once disposed, these queries do not invoke the old
application metadata getter. Successful queries allocate no transaction objects.

## Shared HasChildren descriptors

Each realized expander formerly constructed a new ValueColumn for the same
HasChildren expression. Each such descriptor could compile its own Core getter
on first use. Descriptors are now shared by hierarchical-column identity through
a ConditionalWeakTable; row-specific CellBinding instances and subscriptions stay
independent. A discarded column and its descriptor remain collectible. There is
no strong static model/source/culture cache and no change to the binding's existing
JIT/AOT policy.

Immutable ShowExpander event arguments are also shared. Focused regressions check
zero measuring-thread managed allocation across 4,096 warmed descriptor lookups,
child notifications and permission/visibility query iterations. This does not
mean that constructing an expander allocates nothing, or that all hierarchy
operations now allocate nothing. The standard flat-grid benchmark does not directly
exercise the HasChildren descriptor path, so its timings cannot establish this
optimization's effect on hierarchical workloads.

## Public ColumnList<TModel>

Added `Uno.Controls.Models.TreeDataGrid.ColumnList<TModel>` over the existing
`ColumnListBase<ICellColumn<TModel>>`. This restores the named typed native view
collection without adding a duplicate width solver or source model layer.

The original Avalonia type uses its combined `IColumn<TModel>` contract. The
shared-Core Uno port separates neutral definitions from native presentation:
`ICellColumn<TModel>` owns native layout/cell creation and consumes actual Core
`IRow<TModel>` instances. This is an explicit interface substitution, not a claim
of identical framework ABI. ColumnList is a native view collection, not a
replacement for the Core source's neutral Columns collection.

For example:

```csharp
using Native = Uno.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;

var viewColumns = new Native.ColumnList<RowModel>
{
    new Native.TextColumn<RowModel, string>(
        "Name", row => row.Name, width: new GridLength(160)),
    new Native.CheckBoxColumn<RowModel>(
        "Enabled", row => row.Enabled, width: new GridLength(64)),
};
Native.IColumns layout = viewColumns;
```

Native built-in and custom ICellColumn implementations can coexist in the list.
Pixel/Auto/Star sizing, exact column-edge lookup, collection notifications and
layout invalidation reuse the existing implementation. The list borrows columns:
removing them detaches collection observations but does not dispose caller-owned
columns. Typed CreateCell receives the actual supplied Core row, not a copy.

## Coverage and evidence boundaries

New unit coverage is **30 cases**: 14 hierarchy ownership cases, six typed-list
cases and ten permission/factory boundary cases. One new native suite contains
seven public factory/typed-list scenarios. The latter is also called from the
sequential showcase used by native and published trimmed browser consumers.
No additional friend-assembly or test-only reflection bypass was introduced.

The initial fixture omitted IRow<T>.UpdateModelIndex and did not compile.
`60f59a5b` corrects both new row fixtures. An import typo in the subsequent
permission follow-up was corrected by `9a57ca17`. These intermediate errors
and superseded/cancelled jobs are not presented as successful final execution.

The first completed public-consumer checkpoint `613e4860`, merge `91dd43c4`,
passed 1,279 units, all 50 native suites, sequential integration and Activity
Monitor in run `35910293818`, artifact `10772931646`. This is not substituted
for the later permission/factory changes; the companion checkpoint records the
final-source results and the independent performance gate.

## Remaining acceptance

The typed collection is a real API addition. It does not settle other combined
Core/native declarations, inherited signatures, generated Avalonia exports or
true remaining omissions. Raw API counts are not feature-completion percentages.
The audit remains strict about its unresolved compatibility decisions.

Whole-grid paired performance must still meet the unchanged 1.10 median timing
and allocation ratio budget. Focused allocation checks are not a substitute.
Broader hierarchy, variable-height and mixed-mutation benchmark coverage, physical
input/positive drag, Unicode/IME, screen-reader, DPI and repeated multi-head
runtime acceptance remain necessary. No complete API, full-feature or overall
performance parity claim is made by passing the current fixtures.

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite hierarchy-ownership
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
