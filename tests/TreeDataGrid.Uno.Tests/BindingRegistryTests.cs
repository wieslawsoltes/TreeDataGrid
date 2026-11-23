using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Uno.Controls;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class BindingRegistryTests
{
    [Fact]
    public void Registered_Property_Preserves_Culture_Conversion_And_ReadOnly()
    {
        TreeDataGridBindingRegistry.RegisterProperty<Row, decimal>(nameof(Row.Value), static row => row.Value, static (row, value) => row.Value = value);
        TreeDataGridBindingRegistry.RegisterProperty<Row, string>(nameof(Row.ReadOnly), static row => row.ReadOnly);
        var row = new Row();
        Assert.True(NativeBindingPathWriter.WritePath("Value", row, "1,25", CultureInfo.GetCultureInfo("pl-PL")));
        Assert.Equal(1.25m, row.Value);
        Assert.Throws<InvalidOperationException>(() => NativeBindingPathWriter.WritePath("ReadOnly", row, "other", CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("Children[0].Flag", typeof(bool?))]
    [InlineData("Children[123].Flag", typeof(bool?))]
    [InlineData("Array[0].Flag", typeof(bool?))]
    [InlineData("Values[\"key\"]", typeof(bool?))]
    [InlineData("Child.Flag", typeof(bool?))]
    [InlineData("Child.Missing", null)]
    [InlineData("Child..Flag", null)]
    public void Type_Discovery_Understands_Indexed_Paths_Without_Evaluating_Models(string path, Type? expected)
    {
        RegisterGraph();
        Assert.Equal(expected, NativeBindingPathWriter.GetPathValueType(typeof(Graph), path));
    }

    [Fact]
    public void Registered_Nested_Indexer_Write_Preserves_Nullability_And_Current_Owner()
    {
        RegisterGraph();
        var graph = new Graph();
        graph.Children.Add(new());
        NativeBindingPathWriter.WritePath("Children[0].Flag", graph, "true", CultureInfo.InvariantCulture);
        Assert.True(graph.Children[0].Flag);
        NativeBindingPathWriter.WritePath("Children[0].Flag", graph, string.Empty, CultureInfo.InvariantCulture);
        Assert.Null(graph.Children[0].Flag);
        NativeBindingPathWriter.WritePath("Values[\"key\"]", graph, "false", CultureInfo.InvariantCulture);
        Assert.False(graph.Values["key"]);
    }

    [Fact]
    public void Registered_Indexer_Overloads_Use_Deterministic_Numeric_And_Quoted_Key_Preference()
    {
        TreeDataGridBindingRegistry.RegisterIndexer<Overloads, int, string>(static (row, key) => row.Numbers[key], static (row, key, value) => row.Numbers[key] = value);
        TreeDataGridBindingRegistry.RegisterIndexer<Overloads, string, string>(static (row, key) => row.Text[key], static (row, key, value) => row.Text[key] = value);
        var row = new Overloads();
        NativeBindingPathWriter.WritePath("[1]", row, "numeric", CultureInfo.InvariantCulture);
        NativeBindingPathWriter.WritePath("[\"1\"]", row, "quoted", CultureInfo.InvariantCulture);
        Assert.Equal("numeric", row.Numbers[1]);
        Assert.Equal("quoted", row.Text["1"]);
    }

    [Fact]
    public void Ambiguous_Registered_Indexers_Are_Not_Selected_By_Enumeration_Order()
    {
        TreeDataGridBindingRegistry.RegisterIndexer<Ambiguous, long, string>(static (_, _) => string.Empty);
        TreeDataGridBindingRegistry.RegisterIndexer<Ambiguous, Guid, string>(static (_, _) => string.Empty);
        Assert.Throws<AmbiguousMatchException>(() => NativeBindingPathWriter.WritePath("[1]", new Ambiguous(), "value", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Custom_Conversion_Can_Be_Registered_Without_TypeDescriptor()
    {
        TreeDataGridBindingRegistry.RegisterProperty<ConvertedRow, Measurement>(nameof(ConvertedRow.Value), static row => row.Value, static (row, value) => row.Value = value);
        TreeDataGridBindingRegistry.RegisterConversion<Measurement>(static (value, culture) => new(decimal.Parse((string)value!, culture)));
        var row = new ConvertedRow();
        NativeBindingPathWriter.WritePath("Value", row, "12,5", CultureInfo.GetCultureInfo("pl-PL"));
        Assert.Equal(12.5m, row.Value.Value);
    }

    [Fact]
    public void Empty_Collection_Registration_Preserves_The_Declared_Model_Type()
    {
        TreeDataGridBindingRegistry.RegisterCollection<List<Leaf>, Leaf>();
        Assert.Equal(typeof(Leaf), TreeDataGridBindingRegistry.FindCollectionItemType(typeof(List<Leaf>)));
        Assert.Equal(typeof(Leaf), TreeDataGridBindingRegistry.FindCollectionItemType(typeof(LeafList)));
    }

    [Fact]
    public void Syntax_And_Type_Metadata_Do_Not_Root_Model_Instances()
    {
        RegisterGraph();
        var weak = CreateTransientOwner();
        for (var i = 0; i < 3 && weak.IsAlive; ++i)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        Assert.False(weak.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateTransientOwner()
    {
        var graph = new Graph();
        graph.Children.Add(new());
        NativeBindingPathWriter.WritePath("Children[0].Flag", graph, true, CultureInfo.InvariantCulture);
        return new(graph);
    }

    [Fact]
    public void Repeated_Registered_Path_Type_Discovery_Does_Not_Reparse_Or_Allocate()
    {
        RegisterGraph();
        const string path = "Children[0].Flag";
        for (var i = 0; i < 1024; ++i) NativeBindingPathWriter.GetPathValueType(typeof(Graph), path);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) NativeBindingPathWriter.GetPathValueType(typeof(Graph), path);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Invalid_Registrations_Are_Rejected()
    {
        Assert.Throws<ArgumentException>(() => TreeDataGridBindingRegistry.RegisterProperty<Row, int>("", static _ => 0));
        Assert.Throws<ArgumentException>(() => TreeDataGridBindingRegistry.RegisterProperty<Row, int>("Value", null));
        Assert.Throws<ArgumentException>(() => TreeDataGridBindingRegistry.RegisterIndexer<Row, int, int>(null));
        Assert.Throws<ArgumentNullException>(() => TreeDataGridBindingRegistry.RegisterConversion<int>(null!));
    }

    private static void RegisterGraph()
    {
        TreeDataGridBindingRegistry.RegisterProperty<Graph, List<Leaf>>(nameof(Graph.Children), static row => row.Children);
        TreeDataGridBindingRegistry.RegisterProperty<Graph, Leaf[]>(nameof(Graph.Array), static row => row.Array);
        TreeDataGridBindingRegistry.RegisterProperty<Graph, Dictionary<string, bool?>>(nameof(Graph.Values), static row => row.Values);
        TreeDataGridBindingRegistry.RegisterProperty<Graph, Leaf?>(nameof(Graph.Child), static row => row.Child);
        TreeDataGridBindingRegistry.RegisterProperty<Leaf, bool?>(nameof(Leaf.Flag), static row => row.Flag, static (row, value) => row.Flag = value);
        TreeDataGridBindingRegistry.RegisterIndexer<List<Leaf>, int, Leaf>(static (row, key) => row[key], static (row, key, value) => row[key] = value);
        TreeDataGridBindingRegistry.RegisterIndexer<Dictionary<string, bool?>, string, bool?>(static (row, key) => row[key], static (row, key, value) => row[key] = value);
    }
    private sealed class Row { public decimal Value { get; set; } public string ReadOnly => "fixed"; }
    private sealed class Graph
    {
        public List<Leaf> Children { get; } = new();
        public Leaf[] Array { get; } = [];
        public Dictionary<string, bool?> Values { get; } = new();
        public Leaf? Child { get; set; }
    }
    private sealed class Leaf { public bool? Flag { get; set; } }
    private sealed class LeafList : List<Leaf> { }
    private sealed class Overloads
    {
        internal readonly Dictionary<int, string> Numbers = new();
        internal readonly Dictionary<string, string> Text = new();
    }
    private sealed class Ambiguous { }
    private readonly record struct Measurement(decimal Value);
    private sealed class ConvertedRow { public Measurement Value { get; set; } }
}
