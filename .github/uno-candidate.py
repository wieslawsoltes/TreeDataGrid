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
replace(path, '''        var revision = _revision;
        UpdateRowGeometry(e);
        base.OnItemsCollectionChanged(sender, e);
        if (revision != _revision || IsInLayout) return;
        FinalizeUnrealize();
        RefreshSelection();''', '''        var revision = _revision;
        // A replacement/sort can reuse the cells at the existing display slots.
        // Rebind them before closing deferred cell replacement scopes. Finalizing
        // first reports EndRebind(false), clears template state, and loses the
        // successful replacement hook even when layout reuses the same control.
        // Snapshot only realized slots (including any focus-pinned slot), never
        // the potentially enormous index interval between them.
        var rebind = !IsInLayout && sender is not null &&
            e.Action is NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset
            ? _realized.Keys.Where(index => e.Action == NotifyCollectionChangedAction.Reset ||
                (index >= e.OldStartingIndex && index - e.OldStartingIndex < e.OldItems!.Count))
                .OrderByDescending(index => index).ToArray()
            : Array.Empty<int>();
        UpdateRowGeometry(e);
        base.OnItemsCollectionChanged(sender, e);
        if (revision != _revision || IsInLayout) return;
        try
        {
            // The generic range retires in ascending display order and the row
            // pool is LIFO: consume it in reverse to retain each slot's control.
            foreach (var index in rebind)
            {
                if (revision != _revision || Items is null) return;
                if ((uint)index < (uint)Items.Count && TryRealizeElementAt(index) is null) return;
            }
        }
        finally
        {
            // Actual removal, source replacement, aborted hooks and surplus
            // pooled cells still release their old values immediately.
            if (revision == _revision && !IsInLayout) FinalizeUnrealize();
        }
        if (revision != _revision || IsInLayout) return;
        RefreshSelection();''')
path = 'samples/TreeDataGridUnoSample/RuntimeChecks.cs'
replace(path, '''        items[0] = new Item("Replacement");
        await Task.Delay(100);''', '''        items[0] = new Item("Replacement");
        Check(first.Begins == 1 && first.Ends == 1 && first.LastSucceeded,
            $"Synchronous replacement hooks: begins={first.Begins}, ends={first.Ends}, succeeded={first.LastSucceeded}.");
        await Task.Delay(100);''')
replace(path, '"Replacement hooks are unbalanced."', '$"Replacement hooks are unbalanced: begins={first.Begins}, ends={first.Ends}, succeeded={first.LastSucceeded}."')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
