from pathlib import Path
import json

changed = []
def replace(name, old, new, count=1):
    path = Path(name)
    text = path.read_text()
    assert text.count(old) == count, (name, old, text.count(old))
    path.write_text(text.replace(old, new))
    if name not in changed:
        changed.append(name)

presentation = 'src/TreeDataGrid.Controls.Uno/Presentation/TreeDataGridPresentation.cs'
replace(presentation,
    'private readonly Uno.Controls.Models.TreeDataGrid.ColumnListBase<CellColumn> _visible = new();',
    'private readonly VisibleColumnList _visible = new();')
replace(presentation, '''            _visible.Reset(items =>
            {
                items.Clear();
                foreach (var column in _model.Columns)
                    if (column.IsVisible) items.Add(_views[column].View);
                _selection.ColumnsChanged();
            });''', '''            var next = new List<CellColumn>(_model.Columns.Count);
            foreach (var column in _model.Columns)
                if (column.IsVisible) next.Add(_views[column].View);
            // Publish index maps before observers receive the atomic list change.
            _selection.ColumnsChanged(next);
            _visible.Synchronize(next);''')
selection = 'src/TreeDataGrid.Controls.Uno/Presentation/TreeDataGridSelection.cs'
replace(selection, '    internal void ColumnsChanged()\n    {',
    '    internal void ColumnsChanged(IReadOnlyList<CellColumn>? next = null)\n    {\n        var visible = next ?? columns;')
replace(selection, 'for (var i = 0; i < columns.Count; ++i) _visibleIndexes[columns[i].Model] = i;',
    'for (var i = 0; i < visible.Count; ++i) _visibleIndexes[visible[i].Model] = i;')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
