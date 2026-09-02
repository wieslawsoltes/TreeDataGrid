#!/usr/bin/env python3
"""Check the restored graph and prove that the Core reference gate rejects a UI DLL."""
import json
from pathlib import Path
import subprocess
import tempfile
import xml.sax.saxutils

root = Path(__file__).resolve().parents[1]
for relative in ('src/TreeDataGrid.Core', 'tests/TreeDataGrid.Core.Tests'):
    assets = json.loads((root / relative / 'obj/project.assets.json').read_text())
    ui = [name for name in assets['libraries'] if 'avalonia' in name.lower()]
    if ui:
        raise SystemExit(f'{relative} has UI dependencies: {ui}')
assemblies = sorted((root / 'src/Avalonia.Controls.TreeDataGrid/bin/Release').glob('*/Avalonia.Controls.TreeDataGrid.dll'))
if not assemblies:
    raise SystemExit('Build the Avalonia project in Release before running this check.')
escape = xml.sax.saxutils.escape
with tempfile.TemporaryDirectory(prefix='tdg-neutral-guard-') as directory:
    project = Path(directory) / 'Guard.csproj'
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
<ItemGroup><Reference Include="Avalonia.Controls.TreeDataGrid"><HintPath>{escape(str(assemblies[-1]))}</HintPath></Reference></ItemGroup>
<Import Project="{escape(str(root / 'build/FrameworkNeutral.targets'))}" />
</Project>''')
    result = subprocess.run(['dotnet', 'build', str(project), '--nologo', '-v:q'], capture_output=True, text=True)
    if result.returncode == 0 or 'TreeDataGrid.Core must not reference Avalonia' not in result.stdout + result.stderr:
        raise SystemExit(f'Reference gate did not reject the UI DLL:\n{result.stdout}\n{result.stderr}')
print('Core and Core.Tests have no Avalonia dependencies; the reference gate rejects a UI DLL.')
