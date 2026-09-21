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

rows = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRowsPresenter.cs'
replace(rows, '    private readonly Stack<TreeDataGridRow> _pool = new();',
    '    private readonly List<TreeDataGridRow> _pool = new(32);')
replace(rows, '''        while (_pool.TryPop(out var candidate))
        {
            var reusable = false;''', '''        while (_pool.Count > 0)
        {
            // Keep a retained display slot attached to its original native row
            // across sorting/replacement. Otherwise LIFO reverses the viewport
            // and moves template/focus state to a different visible row.
            // The pool is capped at 32; this scan allocates nothing and does not
            // enumerate source rows or maintain another source-sized index.
            var pooledIndex = _pool.Count - 1;
            for (var i = pooledIndex; i >= 0; --i)
                if (_pool[i].RecycledRowIndex == index && ReferenceEquals(_pool[i].Rows, Items))
                { pooledIndex = i; break; }
            var candidate = _pool[pooledIndex];
            _pool.RemoveAt(pooledIndex);
            var reusable = false;''')
replace(rows, '''    {
        if (_realized.GetValueOrDefault(row.RowIndex) == row) _realized.Remove(row.RowIndex);''', '''    {
        row.RecycledRowIndex = row.RowIndex;
        if (_realized.GetValueOrDefault(row.RowIndex) == row) _realized.Remove(row.RowIndex);''')
replace(rows, 'if (generation == PresenterGeneration) _pool.Push(row);',
    'if (generation == PresenterGeneration) _pool.Add(row);')
replace(rows, '''            if (_pool.Contains(row))
            {
                var remaining = _pool.Where(candidate => !ReferenceEquals(candidate, row)).Reverse().ToArray();
                _pool.Clear();
                foreach (var candidate in remaining) _pool.Push(candidate);
            }''', '''            _pool.Remove(row);''')
replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRow.cs',
    '    internal int RealizationVersion { get; private set; }',
    '    internal int RealizationVersion { get; private set; }\n    internal int RecycledRowIndex { get; set; } = -1;')
replace('samples/TreeDataGridUnoSample/RuntimeChecks.cs',
    'Check(text.Text == "Row 199", "Sort did not refresh retained template content.");',
    'Check(first.RowIndex == 0 && ReferenceEquals(grid.TryGetCell(0, 0), first) && text.Text == "Row 199",\n            $"Sort lost display-slot identity/content: index={first.RowIndex}, text={text.Text}.");')

# Keep audit enabled: promote the vulnerable browser transitive dependency to
# the current stable Microsoft servicing package in both project/package paths.
for project in ('TreeDataGridUnoSample', 'TreeDataGridUnoActivityMonitor'):
    replace(f'samples/{project}/{project}.csproj', '</Project>', '''  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0-browserwasm'">
    <!-- Override the Uno browser toolchain's vulnerable 10.0.2 dependency. -->
    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.12" />
  </ItemGroup>
</Project>''')

workflow = '.github/workflows/uno.yml'
path = Path(workflow)
text = path.read_text()
start = text.index('  windows-app-sdk-build:')
windows = text[start:]
assert windows.count('      - name: Build native Windows samples') == 1
windows = windows.replace('      - name: Build native Windows samples', '''      # Uno's native XAML class-library compiler requires full Visual Studio
      # MSBuild, not the dotnet-hosted build engine (UNOB0008).
      - uses: microsoft/setup-msbuild@v2
      - name: Build native Windows samples''')
for project in ('TreeDataGridUnoSample', 'TreeDataGridUnoActivityMonitor'):
    before = f'dotnet build samples/{project}/{project}.csproj -c Release -f net10.0-windows10.0.26100 -r win-x64'
    after = f'msbuild samples/{project}/{project}.csproj -restore -t:Build -p:Configuration=Release -p:TargetFramework=net10.0-windows10.0.26100 -p:RuntimeIdentifier=win-x64\n          if ($LASTEXITCODE -ne 0) {{ exit $LASTEXITCODE }}'
    assert windows.count(before) == 1
    windows = windows.replace(before, after)
old = 'dotnet pack src/TreeDataGrid.Controls.Uno/TreeDataGrid.Controls.Uno.csproj -c Release -p:PackageVersion=12.0.0.7-ci.${{ github.run_id }} -o artifacts/uno-pack'
new = 'msbuild src/TreeDataGrid.Controls.Uno/TreeDataGrid.Controls.Uno.csproj -restore -t:Pack -p:Configuration=Release -p:PackageVersion=12.0.0.7-ci.${{ github.run_id }} -p:PackageOutputPath="$env:GITHUB_WORKSPACE/artifacts/uno-pack"\n          if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }'
assert windows.count(old) == 1
windows = windows.replace(old, new)
for project, output in (('TreeDataGridUnoSample', 'showcase'), ('TreeDataGridUnoActivityMonitor', 'monitor')):
    before = f'dotnet publish samples/{project}/{project}.csproj -c Release -f net10.0-windows10.0.26100 -r win-x64'
    after = f'msbuild samples/{project}/{project}.csproj -restore -t:Publish -p:Configuration=Release -p:TargetFramework=net10.0-windows10.0.26100 -p:RuntimeIdentifier=win-x64'
    assert windows.count(before) == 1
    windows = windows.replace(before, after)
    before = f'-o artifacts/windows/{output}'
    after = f'-p:PublishDir="$env:GITHUB_WORKSPACE/artifacts/windows/{output}/"\n          if ($LASTEXITCODE -ne 0) {{ exit $LASTEXITCODE }}'
    assert windows.count(before) == 1
    windows = windows.replace(before, after)
path.write_text(text[:start] + windows)
changed.append(workflow)
Path('artifacts/candidate/files.json').write_text(json.dumps(changed))
