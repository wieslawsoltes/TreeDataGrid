using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Windows.Foundation;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Native container observation without changing the model or view lifetime contract.</summary>
internal static class NativeObserverAllocationRuntimeChecks
{
    internal static void Run()
    {
        VerifyCellObservation();
        VerifyHeaderObservation();
        Console.WriteLine("UNO_RUNTIME_OBSERVER_ALLOCATION_PASSED: 4096 warmed cell observation cycles, no managed allocation, single delivery, retained-control reuse, borrowed cleanup and exact header delegate identity");
    }

    private static void VerifyCellObservation()
    {
        var first = new ValueModel("First");
        var second = new ValueModel("Second");
        var control = new ProbeCell();
        var factory = new TreeDataGridElementFactory();
        try
        {
            control.Realize(factory, null, first, 0, 0);
            for (var i = 0; i < 1024; ++i) { control.Stop(); control.Observe(); }
            // Only the protected subscribe/unsubscribe path is measured. No
            // construction, native DP setters, GC or user notifications occur
            // inside this interval; it is not a frame-time benchmark.
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4096; ++i) { control.Stop(); control.Observe(); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, $"Warmed cell observation allocated {allocated} managed bytes.");
            control.Observe(); control.Observe();
            first.Notify();
            Check(control.Notifications == 1, "Repeated observation duplicated current model notifications.");
            control.Stop(); control.Stop(); first.Notify();
            Check(control.Notifications == 1, "Unsubscribed model still notified its control.");
            control.Observe(); first.Notify();
            Check(control.Notifications == 2, "Resubscription lost current model notification.");
            control.Unrealize(); first.Notify();
            Check(control.Notifications == 2 && first.Disposals == 0,
                "Unrealization retained an observer or disposed its borrowed native value.");
            control.Realize(factory, null, second, 3, 7);
            first.Notify(); second.Notify();
            Check(control.Notifications == 3 && ReferenceEquals(control.Model, second) && control.RowIndex == 7 && control.ColumnIndex == 3,
                "A retained container's cached observer targeted its former row.");
            control.Unrealize(); second.Notify();
            Check(control.Notifications == 3 && second.Disposals == 0,
                "Retained-container cleanup lost its borrowed-model ownership boundary.");
        }
        finally { control.Unrealize(); first.Dispose(); second.Dispose(); }
    }

    private static void VerifyHeaderObservation()
    {
        var first = new ColumnModel();
        var second = new ColumnModel();
        var columns = new Columns(first);
        var header = new TreeDataGridColumnHeader();
        PropertyChangedEventHandler? cached = null;
        try
        {
            for (var i = 0; i < 256; ++i)
            {
                var model = (i & 1) == 0 ? first : second;
                columns.Current = model;
                header.Realize(columns, 0);
                cached ??= model.LastAdded;
                Check(ReferenceEquals(cached, model.LastAdded) && model.Subscribers == 1,
                    "Header reuse created a new delegate or duplicated its subscription.");
                model.HeaderValue = "Current " + i;
                model.Notify();
                Check(Equals(header.Header, model.HeaderValue), "The cached header observer did not read its current column.");
                header.Unrealize();
                Check(ReferenceEquals(cached, model.LastRemoved) && model.Subscribers == 0,
                    "Header removal did not use the exact cached delegate or detach the current column.");
                model.Notify();
                Check(header.Header is null && header.ColumnIndex == -1, "A retired header received an old column notification.");
            }
        }
        finally { header.Unrealize(); }
        Check(first.Adds == 128 && second.Adds == 128 && first.Removes == 128 && second.Removes == 128,
            "Header recycling changed application event accessor counts.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class ProbeCell : TreeDataGridCell
    {
        internal int Notifications;
        internal void Stop() => UnsubscribeFromModelChanges();
        internal void Observe() => SubscribeToModelChanges();
        protected override void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ++Notifications;
            base.OnModelPropertyChanged(sender, e);
        }
    }
    private sealed class ValueModel(string value) : CellValue
    {
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Value));
        internal int Disposals;
        public override object? Value => value;
        public override bool CanEdit => false;
        public override void Write(object? next) => throw new NotSupportedException();
        internal void Notify() => RaisePropertyChanged(Changed);
        public override void Dispose() => ++Disposals;
    }
    private sealed class ColumnModel : UI.IColumn
    {
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Header));
        private PropertyChangedEventHandler? _handlers;
        internal PropertyChangedEventHandler? LastAdded, LastRemoved;
        internal int Subscribers, Adds, Removes;
        internal object? HeaderValue = "Header";
        public double ActualWidth => 120;
        public bool? CanUserResize => false;
        public object? Header => HeaderValue;
        public GridLength Width => new(120);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { LastAdded = value; ++Adds; ++Subscribers; _handlers += value; }
            remove { LastRemoved = value; ++Removes; --Subscribers; _handlers -= value; }
        }
        internal void Notify() => _handlers?.Invoke(this, Changed);
    }
    private sealed class Columns(UI.IColumn column) : UI.IColumns
    {
        internal UI.IColumn Current = column;
        public int Count => 1;
        public UI.IColumn this[int index] => index == 0 ? Current : throw new ArgumentOutOfRangeException(nameof(index));
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }
        public event EventHandler? LayoutInvalidated { add { } remove { } }
        public Size CellMeasured(int columnIndex, int rowIndex, Size size) => size;
        public (int index, double x) GetColumnAt(double x) => (0, 0);
        public double GetEstimatedWidth(double constraint) => Current.ActualWidth;
        public void CommitActualWidths() { }
        public void SetColumnWidth(int columnIndex, GridLength width) => throw new NotSupportedException();
        public void ViewportChanged(Rect viewport) { }
        public IEnumerator<UI.IColumn> GetEnumerator() { yield return Current; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
