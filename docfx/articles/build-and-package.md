# Build and Package

New applications should use `TreeDataGrid.Avalonia`, which contains
`TreeDataGrid.Avalonia.dll` and depends directly on the matching
`TreeDataGrid.Core` version. The `TreeDataGrid` package continues to contain
`Avalonia.Controls.TreeDataGrid.dll` for existing applications. The two UI packages
source-link the same implementation; reference only one of them.

## Build and Test

```bash
dotnet restore Avalonia.Controls.TreeDataGrid.slnx
dotnet build Avalonia.Controls.TreeDataGrid.slnx -c Release --no-restore
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj -c Release
```

## Create NuGet Packages

```bash
dotnet pack Avalonia.Controls.TreeDataGrid.slnx -c Release -o artifacts/packages
python3 build/verify-package-layout.py artifacts/packages
```

Output includes:

- `TreeDataGrid.<version>.nupkg`
- `TreeDataGrid.<version>.snupkg`
- `TreeDataGrid.Avalonia.<version>.nupkg`
- `TreeDataGrid.Avalonia.<version>.snupkg`
- `TreeDataGrid.Core.<version>.nupkg`
- `TreeDataGrid.Core.<version>.snupkg`

`TreeDataGrid.Avalonia` uses the
`avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml` theme root. The compatibility
package keeps `avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml`.
