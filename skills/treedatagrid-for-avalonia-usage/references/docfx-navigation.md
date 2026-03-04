# TreeDataGrid DocFX Navigation

## Table of Contents
- [Primary Entry Points](#primary-entry-points)
- [Task Routing Matrix](#task-routing-matrix)
- [Namespace Reference Pages](#namespace-reference-pages)
- [API YAML Lookup Workflow](#api-yaml-lookup-workflow)
- [Fast Search Commands](#fast-search-commands)
- [Legacy Article Stubs](#legacy-article-stubs)

## Primary Entry Points

- `docfx/articles/toc.yml`
- `docfx/articles/reference/api-coverage-index.md`
- `docfx/api/toc.yml`
- `docfx/api/index.md`

Start from these four files before opening lower-level pages.

## Task Routing Matrix

Use this matrix to choose article sources before coding.

| User task | Start with these articles | Add these supporting sources |
|---|---|---|
| Install and first setup | `docfx/articles/getting-started/installation.md`, `docfx/articles/getting-started/overview.md` | `docfx/api/Avalonia.Controls.TreeDataGrid.yml` |
| Flat source configuration | `docfx/articles/getting-started/quickstart-flat.md`, `docfx/articles/guides/sources-flat.md` | `docfx/api/Avalonia.Controls.FlatTreeDataGridSource-1.yml`, `docfx/api/Avalonia.Controls.ITreeDataGridSource-1.yml` |
| Hierarchical source configuration | `docfx/articles/getting-started/quickstart-hierarchical.md`, `docfx/articles/guides/sources-hierarchical.md` | `docfx/api/Avalonia.Controls.HierarchicalTreeDataGridSource-1.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn-1.yml` |
| Text columns | `docfx/articles/guides/column-text.md`, `docfx/articles/concepts/columns-cells-rows.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.TextColumn-2.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.TextColumnOptions-1.yml` |
| CheckBox columns | `docfx/articles/guides/column-checkbox.md`, `docfx/articles/concepts/columns-cells-rows.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumn-1.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumnOptions-1.yml` |
| Template columns | `docfx/articles/guides/column-template.md`, `docfx/articles/guides/templates-and-styling.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.TemplateColumn-1.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.TemplateColumnOptions-1.yml` |
| Expander/hierarchy columns | `docfx/articles/guides/column-expander.md`, `docfx/articles/guides/expansion-and-programmatic-navigation.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn-1.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.IExpanderColumn-1.yml` |
| Sorting and widths | `docfx/articles/guides/sorting-and-column-widths.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.ColumnOptions-1.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.SortableRowsBase-2.yml` |
| Editing gestures | `docfx/articles/guides/editing-and-begin-edit-gestures.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.BeginEditGestures.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.ICellOptions.yml` |
| Row selection | `docfx/articles/guides/selection-row.md`, `docfx/articles/concepts/selection-models.md` | `docfx/api/Avalonia.Controls.Selection.TreeDataGridRowSelectionModel-1.yml`, `docfx/api/Avalonia.Controls.Selection.ITreeDataGridRowSelectionModel-1.yml` |
| Cell selection | `docfx/articles/guides/selection-cell.md`, `docfx/articles/concepts/selection-models.md` | `docfx/api/Avalonia.Controls.Selection.TreeDataGridCellSelectionModel-1.yml`, `docfx/api/Avalonia.Controls.Selection.ITreeDataGridCellSelectionModel-1.yml` |
| Selection internals and batching | `docfx/articles/advanced/selection-internals-and-batch-update.md` | `docfx/api/Avalonia.Controls.Selection.TreeSelectionModelBase-1.yml`, `docfx/api/Avalonia.Controls.Selection.TreeSelectionModelBase-1.BatchUpdateOperation.yml` |
| Programmatic expansion | `docfx/articles/guides/expansion-and-programmatic-navigation.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.HierarchicalRows-1.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.IExpander.yml` |
| Drag and drop rows | `docfx/articles/guides/drag-and-drop-rows.md` | `docfx/api/Avalonia.Controls.TreeDataGridRowDragEventArgs.yml`, `docfx/api/Avalonia.Controls.TreeDataGridRowDropPosition.yml` |
| TreeDataGrid events | `docfx/articles/guides/events-and-user-interaction.md` | `docfx/api/Avalonia.Controls.TreeDataGrid.yml`, `docfx/api/Avalonia.Controls.TreeDataGridCellEventArgs.yml` |
| ItemsSourceView integration | `docfx/articles/guides/working-with-itemssourceview.md` | `docfx/api/Avalonia.Controls.TreeDataGridItemsSourceView-1.yml`, `docfx/api/Avalonia.Controls.TreeDataGridItemsSourceView.yml` |
| Templates and styling | `docfx/articles/guides/templates-and-styling.md` | `docfx/api/Avalonia.Controls.Converters.IndentConverter.yml`, `docfx/api/Avalonia.Controls.Primitives.TreeDataGridTemplateCell.yml` |
| XAML usage patterns | `docfx/articles/xaml/overview.md`, `docfx/articles/xaml/samples-walkthrough.md` | `docfx/api/Avalonia.Controls.TreeDataGrid.yml` |
| Theme usage and customization | `docfx/articles/xaml/theme-usage.md`, `docfx/articles/xaml/theme-customization.md`, `docfx/articles/xaml/theme-resource-keys-reference.md` | `docfx/api/Avalonia.Controls.Primitives.TreeDataGridCell.yml`, `docfx/api/Avalonia.Controls.Primitives.TreeDataGridColumnHeader.yml` |
| ControlTheme overrides/replacement | `docfx/articles/xaml/control-theme-overrides-basedon.md`, `docfx/articles/xaml/control-theme-full-replacement.md` | `docfx/api/Avalonia.Controls.Primitives.TreeDataGridRowsPresenter.yml`, `docfx/api/Avalonia.Controls.Primitives.TreeDataGridCellsPresenter.yml` |
| Performance and virtualization | `docfx/articles/advanced/performance-virtualization-and-realization.md` | `docfx/api/Avalonia.Controls.Primitives.TreeDataGridRowsPresenter.yml`, `docfx/api/Avalonia.Controls.Primitives.TreeDataGridColumnHeadersPresenter.yml` |
| Primitive control internals | `docfx/articles/advanced/primitives-overview.md` | `docfx/api/Avalonia.Controls.Primitives.yml`, `docfx/api/Avalonia.Controls.Primitives.TreeDataGridElementFactory.yml` |
| Custom element factory | `docfx/articles/advanced/custom-element-factory.md` | `docfx/api/Avalonia.Controls.Primitives.TreeDataGridElementFactory.yml` |
| Custom rows/columns pipeline | `docfx/articles/advanced/custom-rows-columns-pipeline.md` | `docfx/api/Avalonia.Controls.Models.TreeDataGrid.SortableRowsBase-2.yml`, `docfx/api/Avalonia.Controls.Models.TreeDataGrid.AnonymousSortableRows-1.yml` |
| Typed binding internals | `docfx/articles/advanced/typed-binding.md` | `docfx/api/Avalonia.Experimental.Data.TypedBinding-1.yml`, `docfx/api/Avalonia.Experimental.Data.Core.TypedBindingExpression-2.yml` |
| Diagnostics/testing strategy | `docfx/articles/advanced/diagnostics-and-testing.md`, `docfx/articles/guides/troubleshooting.md` | `docfx/articles/reference/api-coverage-index.md` |

## Namespace Reference Pages

Load namespace reference pages when the task spans many related symbols:

- `docfx/articles/reference/namespace-avalonia-controls.md`
- `docfx/articles/reference/namespace-models-treedatagrid.md`
- `docfx/articles/reference/namespace-selection.md`
- `docfx/articles/reference/namespace-primitives.md`
- `docfx/articles/reference/namespace-experimental.md`
- `docfx/articles/reference/namespace-converters-and-models.md`

Use `docfx/articles/reference/api-coverage-index.md` to map any public type to a primary article.

## API YAML Lookup Workflow

1. Run the UID finder script:

   ```bash
   python3 skills/treedatagrid-for-avalonia-usage/scripts/find_docfx_api.py <query>
   ```

   Include `--include-toc` only when you need `docfx/api/toc.yml` matches.

   For generic UIDs containing backticks, quote the value:

   ```bash
   python3 skills/treedatagrid-for-avalonia-usage/scripts/find_docfx_api.py 'Avalonia.Controls.Models.TreeDataGrid.TextColumn`2' --exact
   ```

2. Open the returned `docfx/api/*.yml` file.
3. Confirm `uid`, `name`, `fullName`, `type`, and member sections.
4. Cross-link to the narrative article from `api-coverage-index.md`.

If generic types are involved, remember:

- UID form keeps backticks: ``TextColumn`2``.
- File names encode generic arity as `-2`: `TextColumn-2.yml`.

## Fast Search Commands

Use these when you need ad-hoc discovery:

```bash
rg -n "TreeDataGrid|FlatTreeDataGridSource|HierarchicalTreeDataGridSource" docfx/articles
rg -n "^# " docfx/articles/guides docfx/articles/advanced docfx/articles/xaml
rg -n "uid: .*TreeDataGrid" docfx/api
rg -n "BeginEditGestures|TreeDataGridRowDropPosition|TreeSelectionModelBase" docfx/api
```

## Legacy Article Stubs

These files currently exist as compatibility redirects with `Article Moved` content. Prefer the destination pages inside `getting-started/`, `guides/`, `xaml/`, `advanced/`, and `reference/`.

- `docfx/articles/intro.md`
- `docfx/articles/installation.md`
- `docfx/articles/get-started-flat.md`
- `docfx/articles/get-started-hierarchical.md`
- `docfx/articles/column-types.md`
- `docfx/articles/selection.md`
- `docfx/articles/samples.md`
- `docfx/articles/build-and-package.md`
- `docfx/articles/license.md`
