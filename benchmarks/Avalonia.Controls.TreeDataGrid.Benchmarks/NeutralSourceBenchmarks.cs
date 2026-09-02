using Avalonia.Controls.Presentation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Adapters;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Headless;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Core = global::TreeDataGridCore;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
public class NeutralSourceBenchmarks
{
    [Params(false, true)] public bool NeutralSource { get; set; }
    private Row[] _items = null!;
    private TreeDataGridPresentation _flat = null!;
    private IDisposable _flatLifetime = null!;
    private TreeDataGridPresentation _tree = null!;
    private IDisposable _treeLifetime = null!;
    private Action _expand = null!;
    private Action _collapse = null!;
    private ListSortDirection _direction;
    [GlobalSetup]
    public void Setup()
    {
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        _items = Enumerable.Range(0, 1000).Select(x => new Row(1000 - x)).ToArray();
        (_flat, _flatLifetime) = CreateFlat();
        var root = new Row(0) { Children = _items };
        if (NeutralSource)
        {
            var model = new Core.HierarchicalTreeDataGridSource<Row>(new[] { root });
            model.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Row>(new Core.Models.TextColumn<Row, int>("ID", x => x.Id), x => x.Children));
            _tree = new TreeDataGridPresentation<Row>(model);
            _treeLifetime = model;
            _expand = () => model.Expand(0);
            _collapse = () => model.Collapse(0);
        }
        else
        {
            var model = new HierarchicalTreeDataGridSource<Row>(new[] { root });
            model.Columns.Add(new HierarchicalExpanderColumn<Row>(new TextColumn<Row, int>("ID", x => x.Id), x => x.Children));
            _tree = new LegacySourcePresentation(model);
            _treeLifetime = model;
            _expand = () => model.Expand(0);
            _collapse = () => model.Collapse(0);
        }
        _ = _flat.Rows.Count;
        _ = _tree.Rows.Count;
        _flat.Rows.CollectionChanged += (_, _) => { };
        _tree.Rows.CollectionChanged += (_, _) => { };
        _expand(); _collapse();
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        _flat.Dispose(); _tree.Dispose();
        _flatLifetime.Dispose(); _treeLifetime.Dispose();
    }
    [Benchmark]
    public int CreateSourceAndPresentation()
    {
        var (source, lifetime) = CreateFlat();
        var count = source.Rows.Count;
        source.Dispose();
        lifetime.Dispose();
        return count;
    }
    [Benchmark]
    public int SortThousandRows()
    {
        _direction = _direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        _flat.SortBy(_flat.Columns[0], _direction);
        return ((Row)_flat.Rows[0].Model!).Id;
    }
    [Benchmark]
    public int ExpandCollapseThousandRows()
    {
        _expand();
        var count = _tree.Rows.Count;
        _collapse();
        return count;
    }
    private (TreeDataGridPresentation source, IDisposable lifetime) CreateFlat()
    {
        if (NeutralSource)
        {
            var model = new Core.FlatTreeDataGridSource<Row>(_items);
            model.Columns.Add(new Core.Models.TextColumn<Row, int>("ID", x => x.Id));
            model.Columns.Add(new Core.Models.TextColumn<Row, string>("Title", x => x.Title));
            return (new TreeDataGridPresentation<Row>(model), model);
        }
        var source = new FlatTreeDataGridSource<Row>(_items);
        source.Columns.Add(new TextColumn<Row, int>("ID", x => x.Id));
        source.Columns.Add(new TextColumn<Row, string>("Title", x => x.Title));
        // Match native presentation's creation of selection and rows.
        _ = source.Selection;
        return (new LegacySourcePresentation(source), source);
    }
    private sealed class Row
    {
        public Row(int id) { Id = id; Title = $"Row {id}"; }
        public int Id { get; }
        public string Title { get; }
        public IEnumerable<Row>? Children { get; init; }
    }
}
