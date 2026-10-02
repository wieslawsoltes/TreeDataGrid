using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Markup;
using System.Xml.Linq;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

/// <summary>Unprefixed TreeDataGrid XAML: the global namespace registration and the Windows App SDK rewrite.</summary>
public class XamlNamespaceTests
{
    private const string Global = "http://schemas.microsoft.com/winfx/2006/xaml/presentation/global";
    private const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string[] GlobalNamespaces = ["Uno.Controls", "Uno.Controls.Primitives"];
    private static readonly Assembly Library = typeof(global::Uno.Controls.TreeDataGrid).Assembly;

    [Fact]
    public void Controls_and_primitives_are_registered_for_the_global_namespace()
    {
        var definitions = Library.GetCustomAttributes<XmlnsDefinitionAttribute>().ToArray();
        Assert.All(definitions, definition => Assert.Equal(Global, definition.XmlNamespace));
        Assert.Equal(GlobalNamespaces, definitions.Select(d => d.ClrNamespace).Order());
    }

    [Fact]
    public void Global_types_do_not_collide()
    {
        var names = ExportedGlobalTypes().Select(type => type.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Rewrite_type_lists_match_the_assembly()
    {
        var targets = XDocument.Load(TargetsPath());
        string List(string name) => targets.Descendants(name).Single().Value;
        var expected = ExportedGlobalTypes().ToLookup(type => type.Namespace!, type => type.Name);
        Assert.Equal(expected["Uno.Controls"].Order(StringComparer.Ordinal), List("TreeDataGridXamlControlsTypes").Split(';'));
        Assert.Equal(expected["Uno.Controls.Primitives"].Order(StringComparer.Ordinal), List("TreeDataGridXamlPrimitivesTypes").Split(';'));
    }

    [Fact]
    public void Rewrite_adds_prefixes_to_elements_property_elements_attached_properties_and_type_values()
    {
        var output = Rewrite("""
            <Page x:Class="App.MainPage" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Page.Resources>
                <ControlTemplate x:Key="Cell" TargetType="TreeDataGridTextCell"><TextBlock Text="&#xE70F;" /></ControlTemplate>
                <Style TargetType="TreeDataGrid"><Setter Property="TreeDataGrid.Tag" Value="1" /></Style>
                <Style TargetType="Button"><Setter Property="Margin" Value="1" /></Style>
              </Page.Resources>
              <TreeDataGrid x:Name="Grid" Grid.Row="1" TreeDataGridColumns.Tag="2">
                <TreeDataGrid.ColumnDefinitions>
                  <TreeDataGridTextColumn Header="Name" Binding="{Binding Name}" />
                </TreeDataGrid.ColumnDefinitions>
              </TreeDataGrid>
            </Page>
            """)!;

        var root = output.Root!;
        Assert.Equal("using:Uno.Controls", root.Attribute(XNamespace.Xmlns + "tdg")!.Value);
        Assert.Equal("using:Uno.Controls.Primitives", root.Attribute(XNamespace.Xmlns + "tdgp")!.Value);
        XNamespace controls = "using:Uno.Controls";
        var grid = Assert.Single(root.Elements(controls + "TreeDataGrid"));
        Assert.Single(grid.Elements(controls + "TreeDataGrid.ColumnDefinitions")).Elements(controls + "TreeDataGridTextColumn").Single();
        Assert.Equal("2", grid.Attribute(controls + "TreeDataGridColumns.Tag")!.Value);
        Assert.Equal("1", grid.Attribute("Grid.Row")!.Value);
        var targetTypes = root.Descendants().Attributes("TargetType").Select(a => a.Value).ToArray();
        Assert.Equal(["tdgp:TreeDataGridTextCell", "tdg:TreeDataGrid", "Button"], targetTypes);
        Assert.Equal(["tdg:TreeDataGrid.Tag", "Margin"], root.Descendants(XName.Get("Setter", Presentation)).Attributes("Property").Select(a => a.Value));
        Assert.Equal("", root.Descendants(XName.Get("TextBlock", Presentation)).Single().Attribute("Text")!.Value);
    }

    [Fact]
    public void Rewrite_reuses_declared_prefixes_and_keeps_prefixed_xaml()
    {
        var output = Rewrite("""
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:grid="using:Uno.Controls"
                         xmlns:tdgp="using:Other">
              <StackPanel>
                <grid:TreeDataGrid />
                <TreeDataGrid />
                <TreeDataGridRowsPresenter />
              </StackPanel>
            </UserControl>
            """)!;

        var root = output.Root!;
        Assert.Equal(["grid", "tdgp", "tdgp1"], root.Attributes().Where(a => a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.Xmlns).Select(a => a.Name.LocalName).Order());
        Assert.Equal("using:Uno.Controls.Primitives", root.Attribute(XNamespace.Xmlns + "tdgp1")!.Value);
        Assert.Equal(2, root.Descendants(XName.Get("TreeDataGrid", "using:Uno.Controls")).Count());
        Assert.Single(root.Descendants(XName.Get("TreeDataGridRowsPresenter", "using:Uno.Controls.Primitives")));
    }

    [Fact]
    public void Uno_heads_rewrite_only_files_with_type_values()
    {
        const string elementsOnly = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TreeDataGrid><TreeDataGrid.Resources /></TreeDataGrid></Grid>
            """;
        Assert.Null(Rewrite(elementsOnly, typeValuesOnly: true));
        Assert.NotNull(Rewrite(elementsOnly));
        var styled = Rewrite("""
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Style TargetType="TreeDataGridRow"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TreeDataGridRow"><TreeDataGridCellsPresenter /></ControlTemplate></Setter.Value></Setter></Style>
            </ResourceDictionary>
            """, typeValuesOnly: true)!;
        Assert.Equal(["tdgp:TreeDataGridRow", "tdgp:TreeDataGridRow"], styled.Root!.Descendants().Attributes("TargetType").Select(a => a.Value));
        // Uno resolves the elements itself; only the string values change.
        Assert.Single(styled.Root.Descendants(XName.Get("TreeDataGridCellsPresenter", Presentation)));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Rewrite_only_inserts_prefixes_and_declarations(string newLine)
    {
        var source = string.Join(newLine,
            "<Page x:Class=\"App.MainPage\"",
            "      xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"",
            "      xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">",
            "  <!-- <TreeDataGrid /> in a comment stays as written -->",
            "  <Page.Resources>",
            "    <Style TargetType=\" TreeDataGridRow\">",
            "      <Setter Property=\"MinHeight\"\tValue=\"28\" />",
            "    </Style>",
            "  </Page.Resources>",
            "  <TreeDataGrid x:Name=\"Grid\"",
            "                ItemsSource=\"{Binding People}\">",
            "    <TreeDataGrid.ColumnDefinitions><TreeDataGridTextColumn Header=\"&#xE70F; &amp; Name\" /></TreeDataGrid.ColumnDefinitions>",
            "  </TreeDataGrid>",
            "</Page>");

        var output = RewriteText(source)!;

        // Removing exactly what was inserted gives back the source, so every line keeps its number.
        var inserted = new[] { " xmlns:tdg=\"using:Uno.Controls\"", " xmlns:tdgp=\"using:Uno.Controls.Primitives\"", "tdgp:", "tdg:" };
        Assert.Equal(source, inserted.Aggregate(output, (text, insertion) => text.Replace(insertion, "")));
        var lines = output.Split(newLine);
        Assert.StartsWith("<Page xmlns:tdg=\"using:Uno.Controls\" xmlns:tdgp=\"using:Uno.Controls.Primitives\" x:Class=", lines[0]);
        Assert.Equal("    <Style TargetType=\" tdgp:TreeDataGridRow\">", lines[5]);
        Assert.Equal("  <tdg:TreeDataGrid x:Name=\"Grid\"", lines[9]);
        Assert.Equal("    <tdg:TreeDataGrid.ColumnDefinitions><tdg:TreeDataGridTextColumn Header=\"&#xE70F; &amp; Name\" /></tdg:TreeDataGrid.ColumnDefinitions>", lines[11]);
        Assert.Equal("  </tdg:TreeDataGrid>", lines[12]);
        Assert.Contains("<!-- <TreeDataGrid /> in a comment stays as written -->", output);
    }

    [Fact]
    public void Rewrite_leaves_xaml_without_unprefixed_TreeDataGrid_types_unchanged()
    {
        Assert.Null(Rewrite("""<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBlock Text="TreeDataGrid" /></Grid>"""));
        Assert.Null(Rewrite("""<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:t="using:Uno.Controls"><t:TreeDataGrid /></Grid>"""));
    }

    private static XDocument? Rewrite(string xaml, bool typeValuesOnly = false) =>
        RewriteText(xaml, typeValuesOnly) is string text ? XDocument.Parse(text) : null;

    private static string? RewriteText(string xaml, bool typeValuesOnly = false)
    {
        var targets = XDocument.Load(TargetsPath());
        var types = TreeDataGridRewriteImplicitXaml.CreateTypeMap(
            targets.Descendants("TreeDataGridXamlControlsTypes").Single().Value,
            targets.Descendants("TreeDataGridXamlPrimitivesTypes").Single().Value);
        return TreeDataGridRewriteImplicitXaml.Rewrite(xaml, types, typeValuesOnly);
    }

    private static Type[] ExportedGlobalTypes() => Library.GetExportedTypes()
        .Where(type => GlobalNamespaces.Contains(type.Namespace) && !type.IsNested && !type.IsGenericTypeDefinition)
        // Uno's XAML generator emits Uno.Controls.GlobalStaticResources.
        .Where(type => type.GetCustomAttribute<EditorBrowsableAttribute>()?.State != EditorBrowsableState.Never)
        .ToArray();

    // Copied next to the tests: CI builds map source paths, so [CallerFilePath] is not a real path.
    private static string TargetsPath() => Path.Combine(AppContext.BaseDirectory, "Build", "TreeDataGrid.Controls.Uno.targets");
}
