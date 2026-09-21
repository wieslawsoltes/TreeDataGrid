from pathlib import Path
import json

changed = []
def replace(name, old, new, count=1):
    path = Path(name)
    text = path.read_text()
    assert text.count(old) == count, (name, old, text.count(old))
    path.write_text(text.replace(old, new))
    if name not in changed: changed.append(name)

replace('samples/TreeDataGridUnoSample/FocusRuntimeChecks.cs', '''        DependencyObject? Next(FocusNavigationDirection direction) =>
            FocusManager.FindNextElement(direction, new FindNextElementOptions { SearchRoot = grid.RowsPresenter });''', '''        DependencyObject? Next(FocusNavigationDirection direction)
        {
            // FindNextElement is XY-only. TryMoveFocus uses the real platform Tab
            // traversal; restore the origin so forward/reverse assertions remain
            // independent and keep subsequent retention checks on that same cell.
            var root = grid.XamlRoot ?? throw new InvalidOperationException("The focus fixture must be attached.");
            var original = FocusManager.GetFocusedElement(root) as Control
                ?? throw new InvalidOperationException("The fixture requires a focused control.");
            try
            {
                if (!FocusManager.TryMoveFocus(direction, new FindNextElementOptions { SearchRoot = grid.RowsPresenter })) return null;
                return FocusManager.GetFocusedElement(root) as DependencyObject;
            }
            finally { Check(original.Focus(FocusState.Keyboard), "Could not restore the focus origin after Tab traversal."); }
        }''')
replace('samples/TreeDataGridUnoSample/ElementFactoryRuntimeChecks.cs',
    'customRow.LastReason == TreeDataGridRowUnrealizeReason.Recycle',
    'customRow.LastReason == TreeDataGridRowUnrealizeReason.ItemRemoved')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridPresenterBase.cs',
    '                scrollToElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));',
    '''                // A specialized presenter must record the target's actual row
                // height/column width before computing its scroll rectangle.
                // Calling Control.Measure directly bypasses that layout contract.
                MeasureElement(index, scrollToElement, new Size(double.PositiveInfinity, double.PositiveInfinity));''')
name = 'src/TreeDataGrid.Controls.Uno/TreeDataGrid.Input.cs'
text = Path(name).read_text()
start = text.index('    public bool BringCellIntoView(int row, int column)')
end = text.index('    public bool MoveSelection(', start)
replace(name, text[start:end], '''    public bool BringCellIntoView(int row, int column)
    {
        if (_scroll is not { } scroll || _presentation is not { } presentation || (uint)row >= (uint)presentation.Rows.Count ||
            (uint)column >= (uint)_geometry.Count || _presenter is not { } presenter) return false;
        var structure = _textSearchStructureRevision;
        // Measuring a distant variable-height viewport changes both its row
        // positions and ScrollViewer's extent. Converge using committed geometry
        // instead of issuing a second request against a still-stale extent.
        // The bound protects against application callbacks which oscillate layout.
        for (var pass = 0; pass < 8; ++pass)
        {
            var x = _geometry.Start(column);
            var right = x + _geometry.Width(column);
            var y = presenter.GetRowStart(row);
            var height = presenter.GetRowHeight(row);
            var bottom = y + height;
            var horizontal = x < scroll.HorizontalOffset ? x : right > scroll.HorizontalOffset + scroll.ViewportWidth
                ? Math.Max(x, right - scroll.ViewportWidth) : scroll.HorizontalOffset;
            var vertical = y < scroll.VerticalOffset ? y : bottom > scroll.VerticalOffset + scroll.ViewportHeight
                ? Math.Max(y, bottom - scroll.ViewportHeight) : scroll.VerticalOffset;
            _pendingVerticalAnchor = null;
            presenter.CancelPendingAnchor();
            scroll.ChangeView(horizontal, vertical, null, true);
            if (!Current()) return false;
            UpdateViewport();
            if (!Current()) return false;
            UpdateLayout();
            if (!Current()) return false;
            y = presenter.GetRowStart(row);
            height = presenter.GetRowHeight(row);
            var current = _pendingVerticalAnchor ?? scroll.VerticalOffset;
            var visible = height > scroll.ViewportHeight
                ? Math.Abs(y - current) < 0.5
                : y >= current - 0.5 && y + height <= current + scroll.ViewportHeight + 0.5;
            if (visible && presenter.TryGetElement(row) is not null) return true;
        }
        return Current();
        bool Current() => structure == _textSearchStructureRevision && ReferenceEquals(_presentation, presentation) &&
            ReferenceEquals(_presenter, presenter) && ReferenceEquals(_scroll, scroll) &&
            (uint)row < (uint)presentation.Rows.Count && (uint)column < (uint)_geometry.Count;
    }
''')
replace('samples/TreeDataGridUnoSample/ViewportCacheRuntimeChecks.cs',
    '"Shrinking/growing the cache detached or replaced compatible pooled controls."',
    '$"Shrinking/growing cache: oldRows={initial.Count}, newRows={presenter.RealizedRows.Count}, retainedRows={initial.Intersect(presenter.RealizedRows).Count()}, oldCells={cells.Count}, newCells={presenter.RealizedCells.Count}, retainedCells={cells.Intersect(presenter.RealizedCells).Count()}, unloads={unloads}, retainedParents={parents.Count(pair => ReferenceEquals(pair.Key.Parent, pair.Value))}."')
replace('samples/TreeDataGridUnoSample/RowSizingRuntimeChecks.cs',
    '"The last variable-height row was not brought fully into view."',
    '$"Last row: realized={presenter.RealizedCells.Any(x => x.RowIndex == 149)}, top={presenter.GetRowStart(149)}, height={presenter.GetRowHeight(149)}, offset={grid.Scroll!.VerticalOffset}, viewport={grid.Scroll.ViewportHeight}, extent={grid.Scroll.ExtentHeight}."')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
