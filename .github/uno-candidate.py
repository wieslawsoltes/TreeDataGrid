from pathlib import Path
import json

changed = []
def replace(name, old, new, count=1):
    path = Path(name)
    text = path.read_text()
    assert text.count(old) == count, (name, old, text.count(old))
    path.write_text(text.replace(old, new))
    if name not in changed: changed.append(name)

name = 'samples/TreeDataGridUnoSample/MainPage.xaml.cs'
text = Path(name).read_text()
start = text.index('if (Windows.Foundation.Metadata.ApiInformation.IsMethodPresent(')
end = text.index('"SetLiveSetting"))', start) + len('"SetLiveSetting"))')
replace(name, text[start:end], 'if (NativeAutomationCapabilities.SupportsLiveRegions)')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellsPresenter.cs',
    'var factory = ElementFactory ?? throw new InvalidOperationException("Cells require an element factory.");',
    '''// A null row-level factory means inherit the owning grid factory.
        // A TemplateBinding may deliver null after Attach has run, so resolve
        // the effective value at the ownership boundary, not only on attachment.
        var factory = ElementFactory ?? Presenter?.Owner?.ElementFactory
            ?? throw new InvalidOperationException("Cells require an element factory.");''')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
