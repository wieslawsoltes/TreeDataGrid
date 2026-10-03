using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;
using Core = TreeDataGridCore.Models;
using Native = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public package-consumer checks for hierarchy ownership and typed native column collections.</summary>
internal static class HierarchyOwnershipRuntimeChecks
{
    internal static void Run()
    {
        Case("cleanup continues after row removal failure", () =>
        {
            using var fixture = new Fixture();
            var error = new InvalidOperationException("Expected row remove failure");
            fixture.Row.RemoveFailure = error;
            Check(ReferenceEquals(Capture(fixture.Cell.Dispose), error), "The original removal error was replaced.");
            fixture.AssertDetached();
            Check(Capture(() => fixture.Cell.Inner.Write("old")) is ObjectDisposedException,
                "An inner value survived failed outer cleanup.");
        });
        Case("child getter disposal", () =>
        {
            using var fixture = new Fixture(); var old = fixture.Model.Children; var next = new Children();
            fixture.Model.Children = next; fixture.Model.OnRead = fixture.Cell.Dispose;
            var changes = 0; fixture.Cell.PropertyChanged += (_, _) => ++changes; fixture.Model.Notify();
            Check(old.Count == 0 && next.Count == 0 && changes == 0, "A retired child getter reattached or published.");
            fixture.AssertDetached();
        });
        Case("latest nested child source", () =>
        {
            using var fixture = new Fixture(); var old = fixture.Model.Children;
            var obsolete = new Children(); var current = new Children(); fixture.Model.Children = obsolete;
            fixture.Model.OnRead = () => { fixture.Model.Children = current; fixture.Model.Notify(); };
            fixture.Model.Notify();
            Check(old.Count == 0 && obsolete.Count == 0 && current.Count == 1, "The old child getter overwrote a newer source.");
            fixture.Cell.Dispose(); fixture.AssertDetached();
        });
        Case("dispose during child event add", () =>
        {
            using var fixture = new Fixture(); var old = fixture.Model.Children;
            var next = new Children { BeforeAdd = _ => fixture.Cell.Dispose() }; fixture.Model.Children = next;
            fixture.Model.Notify();
            Check(old.Count == 0 && next.Count == 0, "An event add accessor attached after disposal.");
            fixture.AssertDetached();
        });
        Case("observed model identity survives getter replacement", () =>
        {
            using var fixture = new Fixture(); fixture.Row.ThrowOnModelRead = true;
            fixture.Cell.Dispose(); fixture.AssertDetached();
        });
        Case("lazy HasChildren remains model specific", () =>
        {
            using var first = new Fixture(lazy: true); using var second = new Fixture(lazy: true);
            first.Model.OnRead = second.Model.OnRead = () => throw new InvalidOperationException("Lazy children were evaluated.");
            first.Model.HasChildren = false; first.Model.Notify();
            second.Model.HasChildren = true; second.Model.Notify();
            Check(!first.Cell.ShowExpander && second.Cell.ShowExpander, "Independent HasChildren bindings shared model state.");
            first.Cell.Dispose(); second.Cell.Dispose(); first.AssertDetached(); second.AssertDetached();
        });
        Case("typed native collection uses real Core rows", () =>
        {
            var model = new Model(); var row = new Row(model);
            var text = new Native.TextColumn<Model, string>("Name", item => item.Name, width: new GridLength(90));
            var check = new Native.CheckBoxColumn<Model>("Children", item => item.HasChildren, width: new GridLength(40));
            var columns = new Native.ColumnList<Model> { text, check };
            try
            {
                columns.ViewportChanged(new Windows.Foundation.Rect(0, 0, 130, 100)); columns.CommitActualWidths();
                Check(columns[0].ActualWidth == 90 && columns[1].ActualWidth == 40 && columns.GetColumnAt(90).index == 1,
                    "Typed list did not share the native layout contract.");
                var cell = columns[0].CreateCell(row);
                try { Check(cell is Native.ITextCell value && value.Text == model.Name, "Typed cell lost its Core model."); }
                finally { (cell as IDisposable)?.Dispose(); }
                columns.Clear();
                Check(model.Count == 0, "Typed cell retained its model subscription.");
            }
            finally { columns.Clear(); text.Dispose(); check.Dispose(); }
        });
        Console.WriteLine("UNO_RUNTIME_HIERARCHY_OWNERSHIP_PASSED: cases=7; real view factories, failed cleanup, callback retirement, latest child sources, lazy expansion, typed column layout and Core ownership");
    }

    private static void Case(string name, Action action) { action(); Console.WriteLine("UNO_HIERARCHY_OWNERSHIP_CASE_PASSED: " + name); }
    private static Exception? Capture(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Fixture : IDisposable
    {
        internal Model Model { get; } = new();
        internal Row Row { get; }
        internal TreeDataGridCore.HierarchicalTreeDataGridSource<Model> Source { get; }
        internal TreeDataGridPresentation View { get; }
        internal ExpanderCellValue Cell { get; }
        internal Fixture(bool lazy = false)
        {
            Source = new(Array.Empty<Model>());
            var definition = new Core.HierarchicalExpanderColumn<Model>(
                Core.ValueColumn<Model, string>.FromDelegate("Name", static item => item.Name), static item => item.ReadChildren(),
                lazy ? static item => item.HasChildren : null);
            Source.Columns.Add(definition);
            View = TreeDataGridPresentation.Create(Source);
            Row = new(Model);
            Cell = (ExpanderCellValue)((CellColumn)View.Columns[0]).CreateCell(Row);
        }
        internal void AssertDetached()
        {
            Check(Model.Count == 0 && Model.Children.Count == 0 && Row.Count == 0, "A retired public expander retained subscriptions.");
            Check(Source.Rows.Count == 0, "Cell disposal changed the caller-owned Core source.");
        }
        public void Dispose() { try { Cell.Dispose(); } finally { try { View.Dispose(); } finally { Source.Dispose(); } } }
    }
    private class Observable : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs Changed = new(null);
        private PropertyChangedEventHandler? _handlers;
        internal int Count => _handlers?.GetInvocationList().Length ?? 0;
        internal Exception? RemoveFailure;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _handlers += value;
            remove { _handlers -= value; if (RemoveFailure is { } error) throw error; }
        }
        internal void Notify() => _handlers?.Invoke(this, Changed);
    }
    private sealed class Model : Observable
    {
        public string Name => "Original model";
        public bool HasChildren { get; set; } = true;
        internal Children Children = new(); internal Action? OnRead;
        internal Children ReadChildren() { var children = Children; var callback = OnRead; OnRead = null; callback?.Invoke(); return children; }
    }
    private sealed class Row(Model model) : Observable, Core.IExpanderRow<Model>
    {
        internal bool ThrowOnModelRead;
        public Model Model => ThrowOnModelRead ? throw new InvalidOperationException("Obsolete row getter") : model;
        object? Core.IRow.Model => Model;
        public object? Header => null;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
        public bool IsExpanded { get; set; }
        public bool ShowExpander { get; private set; } = true;
        public void UpdateShowExpander(bool value) { ShowExpander = value; Notify(); }
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class Children : IEnumerable<Model>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _handlers;
        internal int Count => _handlers?.GetInvocationList().Length ?? 0;
        internal Action<NotifyCollectionChangedEventHandler>? BeforeAdd;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { BeforeAdd?.Invoke(value!); _handlers += value; }
            remove => _handlers -= value;
        }
        public IEnumerator<Model> GetEnumerator() => ((IEnumerable<Model>)Array.Empty<Model>()).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
