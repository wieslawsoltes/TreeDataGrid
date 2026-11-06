using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Windows.Foundation;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static partial class TypedColumnContractRuntimeChecks
{
    private static void VerifyColumnObservers<TModel>(ICellColumn<TModel> factory, IRow<TModel> row)
    {
        var columns = new U.ColumnList<TModel>();
        var original = new ObserverColumn<TModel>(factory);
        var addFailure = new InvalidOperationException("expected observer attachment failure");
        var rejected = new ObserverColumn<TModel>(factory) { AddFailure = addFailure };
        columns.Add(original);
        Exception? failure = null;
        try { columns[0] = rejected; }
        catch (Exception error) { failure = error; }
        Check(ReferenceEquals(failure, addFailure) && ReferenceEquals(columns[0], original) &&
            original.Subscribers == 1 && rejected.Subscribers == 0,
            "Failed observer attachment retired the original column or lost exception identity.");
        VerifyPair(((IReadOnlyList<U.IColumn<TModel>>)columns)[0], original, row);
        Check(ReferenceEquals(original.LastRow, row) && original.Creates == 2,
            "Recovery did not use the actual Core row and independent native cell factories.");

        var second = new ObserverColumn<TModel>(factory);
        var survivor = new ObserverColumn<TModel>(factory);
        var firstFailure = new InvalidOperationException("expected first observer cleanup failure");
        var secondFailure = new InvalidOperationException("expected second observer cleanup failure");
        columns.Add(second);
        columns.Add(original);
        original.RemoveFailure = firstFailure;
        original.RetainOnRemove = true;
        second.RemoveFailure = secondFailure;
        original.OnRemove = () =>
        {
            Check(columns.Count == 0, "Observer removal saw an uncommitted Clear.");
            columns.Add(survivor);
        };
        failure = null;
        try { columns.Clear(); }
        catch (Exception error) { failure = error; }
        Check(failure is AggregateException aggregate && aggregate.InnerExceptions.Count == 2 &&
            ReferenceEquals(aggregate.InnerExceptions[0], firstFailure) &&
            ReferenceEquals(aggregate.InnerExceptions[1], secondFailure),
            "Clear did not attempt both observers or preserve ordered exception identity.");
        Check(columns.Count == 1 && ReferenceEquals(columns[0], survivor) &&
            original.Removes == 1 && second.Removes == 1 && second.Subscribers == 0 && survivor.Subscribers == 1,
            "Old observer cleanup removed a reentrant replacement or detached duplicates twice.");
        columns.CellMeasured(0, 0, new Size(80, 20));
        columns.CommitActualWidths();
        Check(columns.GetColumnAt(1) == (0, 0d), "Replacement column geometry was not committed.");
        var reads = survivor.ActualReads;
        original.Notify();
        Check(columns.GetColumnAt(1) == (0, 0d) && reads == survivor.ActualReads,
            "A publisher-retained retired handler invalidated replacement geometry.");
        VerifyPair(((IReadOnlyList<U.IColumn<TModel>>)columns)[0], survivor, row);

        survivor.OnRemove = () =>
        {
            survivor.OnRemove = null;
            Check(columns.Count == 0, "Reentrant removal saw the retired collection entry.");
            columns.Add(survivor);
        };
        columns.RemoveAt(0);
        Check(columns.Count == 1 && ReferenceEquals(columns[0], survivor) && survivor.Subscribers == 1,
            "Readding the same factory lost current observer ownership.");
        columns.Clear();
        Check(survivor.Subscribers == 0 && original.Disposals + second.Disposals + survivor.Disposals == 0,
            "The collection disposed a borrowed column or retained a healthy observer.");

        var rangeFirst = new ObserverColumn<TModel>(factory) { RemoveFailure = firstFailure };
        var rangeSecond = new ObserverColumn<TModel>(factory) { RemoveFailure = secondFailure };
        columns.Add(rangeFirst);
        columns.Add(rangeSecond);
        var completeNotification = false;
        columns.CollectionChanged += (_, e) => completeNotification =
            e.Action == NotifyCollectionChangedAction.Remove && e.OldItems?.Count == 2 && columns.Count == 0;
        failure = null;
        try { columns.RemoveRange(0, 2); }
        catch (Exception error) { failure = error; }
        Check(failure is AggregateException rangeErrors && rangeErrors.InnerExceptions.Count == 2 &&
            ReferenceEquals(rangeErrors.InnerExceptions[0], firstFailure) &&
            ReferenceEquals(rangeErrors.InnerExceptions[1], secondFailure) && completeNotification &&
            rangeFirst.Subscribers + rangeSecond.Subscribers == 0,
            "A failed observer removal interrupted the range mutation or hid its notification.");
        Console.WriteLine("UNO_RUNTIME_COLUMN_OBSERVER_LIFETIME_PASSED: failed attachment recovery, actual Core rows/native values, duplicate ownership, independent cleanup, retired-event isolation, reentrant readdition and complete range notification");
    }

    private sealed class ObserverColumn<TModel>(ICellColumn<TModel> factory) : ICellColumn<TModel>, IDisposable
    {
        private PropertyChangedEventHandler? _handlers;
        internal Exception? AddFailure, RemoveFailure;
        internal Action? OnRemove;
        internal bool RetainOnRemove;
        internal int Creates, Removes, Disposals, ActualReads;
        internal IRow<TModel>? LastRow;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public double ActualWidth { get { ++ActualReads; return 80; } }
        public bool? CanUserResize => true;
        public object? Header => "Observer lifetime";
        public GridLength Width { get; private set; } = new(80);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => Width = width;
        public Comparison<TModel?>? GetComparison(ListSortDirection direction) => factory.GetComparison(direction);
        public U.ICell CreateCell(IRow<TModel> row) { ++Creates; LastRow = row; return factory.CreateCell(row); }
        public bool TryReuseCell(U.ICell cell, IRow<TModel> row) => factory.TryReuseCell(cell, row);
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _handlers += value; if (AddFailure is { } error) throw error; }
            remove
            {
                ++Removes;
                if (!RetainOnRemove) _handlers -= value;
                OnRemove?.Invoke();
                if (RemoveFailure is { } error) throw error;
            }
        }
        internal void Notify() => _handlers?.Invoke(this, new(nameof(ActualWidth)));
        public void Dispose() => ++Disposals;
    }
}
