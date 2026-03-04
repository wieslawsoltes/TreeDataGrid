# Validation Snippets

This page provides small smoke-test snippets and commands to validate common TreeDataGrid setups.

## Flat Source Smoke Test

```csharp
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

public sealed class Person
{
    public string? Name { get; set; }
    public int Age { get; set; }
}

public sealed class FlatVm
{
    private readonly ObservableCollection<Person> _items = new()
    {
        new() { Name = "Ava", Age = 31 },
        new() { Name = "Mia", Age = 44 },
    };

    public FlatVm()
    {
        Source = new FlatTreeDataGridSource<Person>(_items)
        {
            Columns =
            {
                new TextColumn<Person, string>("Name", x => x.Name, (m, v) => m.Name = v),
                new TextColumn<Person, int>("Age", x => x.Age, (m, v) => m.Age = v ?? 0),
            },
        };
    }

    public FlatTreeDataGridSource<Person> Source { get; }
}
```

## Hierarchical Source Smoke Test

```csharp
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

public sealed class Node
{
    public string? Name { get; set; }
    public ObservableCollection<Node> Children { get; } = new();
}

public sealed class HierVm
{
    private readonly ObservableCollection<Node> _roots = new()
    {
        new()
        {
            Name = "Root",
            Children =
            {
                new() { Name = "Child" },
            },
        },
    };

    public HierVm()
    {
        Source = new HierarchicalTreeDataGridSource<Node>(_roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<Node>(
                    inner: new TextColumn<Node, string>("Name", x => x.Name),
                    childSelector: x => x.Children),
            },
        };

        Source.Expand(new IndexPath(0));
    }

    public HierarchicalTreeDataGridSource<Node> Source { get; }
}
```

## XAML Bind Smoke Test

```xml
<TreeDataGrid Source="{Binding Source}" />
```

If nothing appears:

- verify DataContext assignment
- verify package theme include
- verify source has columns/items

## Runtime Lifecycle Probe Snippet

```csharp
grid.CellPrepared += (_, e) => Console.WriteLine($"CellPrepared {e.ColumnIndex}:{e.RowIndex}");
grid.RowPrepared += (_, e) => Console.WriteLine($"RowPrepared {e.RowIndex}");
```

Use this to confirm realization is happening as expected.

## CI/Local Validation Commands

```bash
dotnet restore Avalonia.Controls.TreeDataGrid.slnx
dotnet build Avalonia.Controls.TreeDataGrid.slnx -c Release --no-restore
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj -c Release
./check-docs.sh
```

## Troubleshooting

- Snippet compiles but behavior differs in app
Cause: app-level styles, DataContext lifecycle, or custom source logic differ from the minimal sample.
Fix: compare app setup against Installation and Troubleshooting guides.

- Runtime interaction differs from expectations
Cause: active selection mode or sorted/hierarchical source semantics.
Fix: verify selection/source configuration and run targeted feature guide examples.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.FlatTreeDataGridSource`1>
- <xref:Avalonia.Controls.HierarchicalTreeDataGridSource`1>

## Related

- [Quickstart: Flat TreeDataGrid](../getting-started/quickstart-flat.md)
- [Quickstart: Hierarchical TreeDataGrid](../getting-started/quickstart-hierarchical.md)
- [Troubleshooting Guide](troubleshooting.md)
- [Diagnostics and Testing](../advanced/diagnostics-and-testing.md)
