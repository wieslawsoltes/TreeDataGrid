# Diagnostics and Testing

This article covers practical workflows for validating TreeDataGrid behavior in development and CI.

## Run the Sample App

```bash
dotnet run --project samples/TreeDataGridDemo/TreeDataGridDemo.csproj
```

Use sample scenarios to validate:

- sorting, resizing, and editing interactions
- row and cell selection behavior
- expansion/collapse in hierarchical mode
- row drag/drop scenarios

## Build, Test, and Docs

```bash
dotnet restore Avalonia.Controls.TreeDataGrid.slnx
dotnet build Avalonia.Controls.TreeDataGrid.slnx -c Release --no-restore
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj -c Release
./check-docs.sh
```

## Package Validation

```bash
dotnet pack src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj -c Release -o artifacts/packages
```

Before publishing, verify:

- package contains expected assemblies and themes
- no public API regressions
- docs site builds without warnings

## Instrumenting Runtime Events

Use lifecycle events to inspect realization/edit paths:

```csharp
grid.CellPrepared += (_, e) => Console.WriteLine($"CellPrepared C={e.ColumnIndex}, R={e.RowIndex}");
grid.CellClearing += (_, e) => Console.WriteLine($"CellClearing C={e.ColumnIndex}, R={e.RowIndex}");
grid.RowPrepared += (_, e) => Console.WriteLine($"RowPrepared R={e.RowIndex}");
grid.RowClearing += (_, e) => Console.WriteLine($"RowClearing R={e.RowIndex}");
```

This helps detect over-realization, incorrect reuse, and unexpected re-entry.

## Regression Areas to Test

- source replacement (`Source` swap, `Items` replacement)
- sort then selection consistency
- expand/collapse with selection preservation
- drag/drop with sorted and unsorted constraints
- virtualization correctness at high row counts

## Troubleshooting

- flaky selection tests
Cause: asserting visuals instead of model indexes; prefer model-level assertions first.

- drag/drop tests fail only in sorted mode
Cause: sorted sources intentionally block automatic drag/drop move paths.

- docs CI failures
Cause: unresolved links or xrefs after API/article refactor.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.TreeDataGridCellEventArgs>
- <xref:Avalonia.Controls.TreeDataGridRowEventArgs>

## Related

- [Events and User Interaction](../guides/events-and-user-interaction.md)
- [Drag and Drop Rows](../guides/drag-and-drop-rows.md)
- [Validation Snippets](../guides/validation-snippets.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
- [Performance, Virtualization, and Realization](performance-virtualization-and-realization.md)
- [API Coverage Index](../reference/api-coverage-index.md)
