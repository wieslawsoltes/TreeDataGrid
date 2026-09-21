from pathlib import Path
import json

changed = []
def replace(name, old, new, count=1):
    path = Path(name)
    text = path.read_text()
    assert text.count(old) == count, (name, old, text.count(old))
    path.write_text(text.replace(old, new))
    if name not in changed: changed.append(name)

replace('samples/TreeDataGridUnoSample/FocusRuntimeChecks.cs',
    'new FindNextElementOptions { SearchRoot = grid.RowsPresenter }',
    'new FindNextElementOptions { SearchRoot = root.Content }')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridPresenterBase.cs',
    'sender != _focusedElement', '!ReferenceEquals(sender, _focusedElement)')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellsPresenter.cs', '''            if (nativeColumn is not null)
                value = TakeRetainedModel(nativeColumn, presentation!, index, rowIndex) ?? presentation!.RealizeCell(index, rowIndex);
            model = value?.PresentationModel ?? rows.RealizeCell(column, index, rowIndex);''', '''            if (nativeColumn is not null)
            {
                value = TakeRetainedModel(nativeColumn, presentation!, index, rowIndex);
                // A custom reuse callback may replace/dispose the entire source.
                // Check retirement before attempting a fresh cell in the old view.
                EnsureGeneration(generation);
                value ??= presentation!.RealizeCell(index, rowIndex);
                EnsureGeneration(generation);
            }
            model = value?.PresentationModel ?? rows.RealizeCell(column, index, rowIndex);''')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridColumnarPresenterBase.cs', '''            MeasureNative(element, availableSize);
            return Columns!.CellMeasured(index, rowIndex, element.DesiredSize);''', '''            var generation = PresenterGeneration;
            var columns = Columns!;
            MeasureNative(element, availableSize);
            // Template construction and custom MeasureOverride are user code.
            // They may retire this layout and clear its column collection.
            EnsureGeneration(generation);
            return columns.CellMeasured(index, rowIndex, element.DesiredSize);''')
replace('samples/TreeDataGridUnoSample/CustomReuseRuntimeChecks.cs',
    'try { items[0] = new("Throwing reuse"); }',
    'try { items[0] = new("Throwing reuse"); grid.UpdateLayout(); }')
for name in ['ColumnCompatibilityRuntimeChecks.cs', 'SourceExtensionsRuntimeChecks.cs']:
    replace('samples/TreeDataGridUnoSample/' + name,
        'ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault()?.Text',
        'ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault(text => text.Visibility == Visibility.Visible)?.Text')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellsPresenter.cs',
    'if (RowIndex < 0 || _row?.IsResettingCells == true || Rows is null',
    'if (_resettingCells || RowIndex < 0 || _row?.IsResettingCells == true || Rows is null')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
