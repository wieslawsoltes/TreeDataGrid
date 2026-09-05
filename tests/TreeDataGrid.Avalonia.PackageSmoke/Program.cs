using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using TreeDataGridCore.Models;

AppBuilder.Configure<Application>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
    .SetupWithoutStarting();

var source = new TreeDataGridCore.FlatTreeDataGridSource<Row>(new[] { new Row("TreeDataGrid.Controls.Avalonia") })
{
    Columns =
    {
        new TextColumn<Row, string>("Name", row => row.Name),
    },
};
var grid = new TreeDataGrid { Model = source };

if (!ReferenceEquals(grid.Model, source))
    throw new InvalidOperationException("The Avalonia control did not accept the Core model.");
if (typeof(TreeDataGrid).Assembly.GetName().Name != "TreeDataGrid.Avalonia")
    throw new InvalidOperationException("The compatibility UI assembly was resolved instead of TreeDataGrid.Avalonia.");
if (typeof(TreeDataGridCore.FlatTreeDataGridSource<>).Assembly.GetName().Name != "TreeDataGrid.Core")
    throw new InvalidOperationException("TreeDataGrid.Core was not resolved transitively.");

var theme = AvaloniaXamlLoader.Load(
    new Uri("avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml"));
if (theme is not Styles)
    throw new InvalidOperationException("The TreeDataGrid.Avalonia theme did not load as Avalonia styles.");

Console.WriteLine("TreeDataGrid.Controls.Avalonia package smoke test passed.");

internal sealed record Row(string Name);
