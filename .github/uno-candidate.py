from pathlib import Path
import json

changed = []
def replace(name, old, new, count=1):
    path = Path(name)
    text = path.read_text()
    assert text.count(old) == count, (name, old, text.count(old))
    path.write_text(text.replace(old, new))
    if name not in changed: changed.append(name)

def wrap(name, start, end, alternative):
    path = Path(name)
    text = path.read_text()
    first = text.index(start)
    last = text.index(end, first) + len(end)
    old = text[first:last]
    replace(name, old, '#if __WASM__\n' + alternative + '\n#else\n' + old + '\n#endif')

replace('samples/TreeDataGridUnoSample/FocusRuntimeChecks.cs',
    'FocusManager.FindNextElement(direction, new FindNextElementOptions { SearchRoot = grid.RowsPresenter })',
    'FocusManager.FindNextElement(direction)')
for name in ['samples/TreeDataGridUnoSample/App.xaml.cs',
             'samples/TreeDataGridUnoSample/App.Validation.cs',
             'samples/TreeDataGridUnoActivityMonitor/App.xaml.cs']:
    path = Path(name)
    text = path.read_text()
    old = 'if (!OperatingSystem.IsBrowser()) Exit();'
    assert old in text
    text = text.replace(old, '\n#if !__WASM__\n            Exit();\n#endif')
    path.write_text(text)
    changed.append(name)
wrap('samples/TreeDataGridUnoSample/App.xaml.cs',
     '        var bitmap = new RenderTargetBitmap();',
     '        Console.WriteLine($"UNO_SCREENSHOT: {path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");',
     '        throw new PlatformNotSupportedException("The DOM renderer requires browser-driver screenshots; RenderTargetBitmap is unavailable.");')
wrap('samples/TreeDataGridUnoActivityMonitor/ActivityMonitorRuntimeChecks.cs',
     '        var bitmap = new RenderTargetBitmap();',
     '        Console.WriteLine($"UNO_ACTIVITY_SCREENSHOT: {path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");',
     '        await Task.CompletedTask;\n        throw new PlatformNotSupportedException("The DOM renderer requires browser-driver screenshots; RenderTargetBitmap is unavailable.");')
replace('samples/TreeDataGridUnoSample/AppearanceRuntimeChecks.cs',
    '            grid.FlowDirection = FlowDirection.RightToLeft;',
    '            ConfigureRightToLeft(grid);')
replace('samples/TreeDataGridUnoSample/AppearanceRuntimeChecks.cs',
    '    internal static async Task RunAsync(MainPage page)', '''    private static void ConfigureRightToLeft(FrameworkElement element)
    {
#if __WASM__
        throw new PlatformNotSupportedException("The DOM renderer does not implement the native FlowDirection contract; this RTL gate requires an implemented head.");
#else
        element.FlowDirection = FlowDirection.RightToLeft;
#endif
    }

    internal static async Task RunAsync(MainPage page)''')
replace('samples/TreeDataGridUnoSample/MainPage.xaml.cs',
    '"Microsoft.UI.Xaml.Automation.AutomationProperties", "SetLiveSetting"',
    '''
#if WINDOWS
                "Microsoft.UI.Xaml.Automation.AutomationProperties",
#else
                typeof(Microsoft.UI.Xaml.Automation.AutomationProperties).AssemblyQualifiedName!,
#endif
                "SetLiveSetting"''')
replace('tools/TreeDataGrid.ApiAudit/Program.cs',
    'var baseline = Surface.Read([baselinePath, corePath]);\n    var target = Surface.Read([targetPath, corePath]);',
    '''var baseline = Surface.Read([baselinePath, corePath], Environment.GetEnvironmentVariable("TREEDATAGRID_API_BASELINE_REFERENCES"));
    var target = Surface.Read([targetPath, corePath], Environment.GetEnvironmentVariable("TREEDATAGRID_API_TARGET_REFERENCES"));''')
replace('tools/TreeDataGrid.ApiAudit/Program.cs',
    'public static Surface Read(string[] inputs)', 'public static Surface Read(string[] inputs, string? referenceDirectories)')
replace('tools/TreeDataGrid.ApiAudit/Program.cs',
    '        foreach (var input in inputs) AddReference(input);', '''        foreach (var input in inputs) AddReference(input);
        foreach (var directory in (referenceDirectories ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            foreach (var file in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.Ordinal)) AddReference(file);
        }''')
replace('tools/TreeDataGrid.ApiAudit/Program.cs',
    '    Console.WriteLine("UNO_API_AUDIT=" + JsonSerializer.Serialize(report));', '''    Console.WriteLine("UNO_API_AUDIT=" + JsonSerializer.Serialize(new
    {
        report.baselineShapes, report.targetShapes, report.exactNormalizedMatches,
        report.missingOrDifferent, report.additionalOrDifferent,
        unresolvedBaselineTypes = baseline.UnresolvedTypes.Length,
        unresolvedTargetTypes = target.UnresolvedTypes.Length,
        report.completeApiParityProven,
    }));''')
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
