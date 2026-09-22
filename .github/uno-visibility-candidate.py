from pathlib import Path
import hashlib
import json

changed = []
def replace(path, old, new, count=1):
    file = Path(path)
    text = file.read_text()
    assert text.count(old) == count, (path, old, text.count(old))
    file.write_text(text.replace(old, new))
    if path not in changed:
        changed.append(path)

base = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridPresenterBase.cs'
replace(base, '                    element.Visibility = Visibility.Collapsed;',
        '                    if (!PreserveRecycledElementVisibility(element)) element.Visibility = Visibility.Collapsed;')
replace(base, '                element.Visibility = Visibility.Collapsed;',
        '                if (!PreserveRecycledElementVisibility(element)) element.Visibility = Visibility.Collapsed;')
replace(base, '        protected virtual bool OwnsRecyclingPool => false;', '''        protected virtual bool OwnsRecyclingPool => false;

        /// <summary>
        /// Allows a row-owned pool to avoid a redundant local visibility change
        /// while the whole parent row is being retired. The owner must hide the
        /// parent synchronously and finalize unused children before it is reused.
        /// Other containers and standalone presenters keep normal recycling.
        /// </summary>
        protected virtual bool PreserveRecycledElementVisibility(Control element) => false;''')
cells = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellsPresenter.cs'
replace(cells, '    protected override bool OwnsRecyclingPool => true;', '''    protected override bool OwnsRecyclingPool => true;
    protected override bool PreserveRecycledElementVisibility(Control element) =>
        // Row.Unrealize hides the whole parent synchronously after its cells
        // retire. Retained cell content already belongs to a balanced rebind
        // scope; EndRebind(false) collapses unused cells at finalization. Do not
        // defer horizontal-only recycling in a row that stays visible.
        _deferRowRebind && element is TreeDataGridCell cell && _deferred.Contains(cell);''')
registration = 'samples/TreeDataGridUnoSample/App.Validation.cs'
replace(registration, '            case "cross-column-recycling": return CrossColumnRecyclingRuntimeChecks.RunAsync(page.Grid);',
    '''            case "cross-column-recycling": return CrossColumnRecyclingRuntimeChecks.RunAsync(page.Grid);
            case "row-recycling-visibility": return RowRecyclingVisibilityRuntimeChecks.RunAsync(page.Grid);''')
Path('artifacts/visibility-candidate').mkdir(parents=True, exist_ok=True)
Path('artifacts/visibility-candidate/files.json').write_text(json.dumps(changed))
Path('artifacts/visibility-candidate/sha256.json').write_text(json.dumps({path: hashlib.sha256(Path(path).read_bytes()).hexdigest() for path in changed}, indent=2))
