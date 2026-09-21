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

replace('samples/TreeDataGridUnoSample/App.xaml.cs',
    '            await Task.Delay(1500);',
    '            await Task.Delay(1500);\n            if (await TryRunSelectedSuiteAsync(page)) return;')
replace('tools/TreeDataGrid.ApiAudit/Program.cs',
    'member is IMethodSymbol method && method.MethodKind is',
    'member is IMethodSymbol accessor && accessor.MethodKind is')
replace('tests/TreeDataGrid.Uno.Tests/ColumnListCompatibilityTests.cs', '''        var notifications = 0;
        columns.CollectionChanged += (_, e) =>
        {
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
            ++notifications;
            Assert.Equal(columns.Count, presentation.NativeColumns.Count);
        };
        source.Columns[1].IsVisible = true;
        Assert.Equal(1, notifications);
        Assert.Equal(2, columns.Count);
        source.Columns.Move(1, 0);
        Assert.Equal(2, notifications);''', '''        var notifications = new List<NotifyCollectionChangedAction>();
        NotifyCollectionChangedEventHandler handler = (_, e) =>
        {
            notifications.Add(e.Action);
            Assert.Equal(columns.Count, presentation.NativeColumns.Count);
        };
        columns.CollectionChanged += handler;
        source.Columns[1].IsVisible = true;
        Assert.Equal(NotifyCollectionChangedAction.Add, Assert.Single(notifications));
        Assert.Equal(2, columns.Count);
        source.Columns.Move(1, 0);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Move }, notifications);
        columns.CollectionChanged -= handler;''')
replace('samples/TreeDataGridUnoSample/RuntimeChecks.cs',
    '        var originalColumn = retained.Column!;', '''        var originalText = Descendants(retained).OfType<TextBlock>().Single(x => x.Name == "PART_Text");
        var originalAlignment = originalText.TextAlignment;
        var originalWrapping = originalText.TextWrapping;
        var originalTrimming = originalText.TextTrimming;
        var originalColumn = retained.Column!;''')
replace('samples/TreeDataGridUnoSample/RuntimeChecks.cs', '''        Check(alignedText.TextAlignment == TextAlignment.Left && alignedText.TextWrapping == TextWrapping.NoWrap &&
            alignedText.TextTrimming == TextTrimming.None, "Rebinding retained the previous column's text options.");''', '''        Check(alignedText.TextAlignment == originalAlignment && alignedText.TextWrapping == originalWrapping &&
            alignedText.TextTrimming == originalTrimming, "Rebinding retained the previous column's text options instead of restoring the template defaults.");''')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
