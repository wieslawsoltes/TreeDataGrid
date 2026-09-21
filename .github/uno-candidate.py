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

path = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRowsPresenter.cs'
replace(path, '''        base.OnItemsCollectionChanged(sender, e);
        if (revision != _revision || IsInLayout) return;
        FinalizeUnrealize();
        RefreshSelection();''', '''        base.OnItemsCollectionChanged(sender, e);
        if (revision != _revision || IsInLayout) return;
        // Replacement/sort scopes finish when the next layout rebinds retained
        // rows. Closing them here sends EndRebind(false) before reuse and clears
        // retained template state. Do not eagerly realize rows inside Reset:
        // a source may still be completing its items/selection transaction.
        // Source detachment, empty collections and actual removals must release
        // ownership now; surplus recycled rows are finalized after measurement.
        if (sender is null || Items is null || Items.Count == 0 ||
            e.Action is not (NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset))
            FinalizeUnrealize();
        RefreshSelection();''')
path = 'samples/TreeDataGridUnoSample/RuntimeChecks.cs'
replace(path, '"Replacement hooks are unbalanced."', '$"Replacement hooks are unbalanced: begins={first.Begins}, ends={first.Ends}, succeeded={first.LastSucceeded}."')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
