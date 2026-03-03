# Build and Package

## Build and Test

```bash
dotnet restore Avalonia.Controls.TreeDataGrid.slnx
dotnet build Avalonia.Controls.TreeDataGrid.slnx -c Release --no-restore
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj -c Release
```

## Create NuGet Packages

```bash
dotnet pack src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj -c Release -o artifacts/packages
```

Output includes:

- `TreeDataGrid.<version>.nupkg`
- `TreeDataGrid.<version>.snupkg`
