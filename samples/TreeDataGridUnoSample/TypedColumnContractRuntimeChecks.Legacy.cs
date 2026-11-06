using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static partial class TypedColumnContractRuntimeChecks
{
    private static void VerifyLegacyFactories<TModel>(ICellColumn<TModel> original, IRow<TModel> row)
    {
        var raw = new LegacyOnlyColumn<TModel>(original);
        var explicitColumn = new ExplicitFactoryColumn<TModel>(original);
        var columns = new U.ColumnList<TModel> { raw, explicitColumn, raw };
        try
        {
            IReadOnlyList<U.IColumn<TModel>> typed = columns;
            var facade = typed[0];
            Check(ReferenceEquals(columns[0], raw) && ReferenceEquals(((U.IColumns)columns)[0], raw),
                "The typed facade replaced the mutable/native column identity.");
            Check(ReferenceEquals(facade, typed[2]) && ReferenceEquals(typed[1], explicitColumn),
                "Duplicate legacy projections or typed columns changed identity.");
            Check(raw.Subscriptions == 1, "A read-only projection subscribed to or duplicated its legacy column.");
            Check(facade.GetComparison(ListSortDirection.Ascending) is null &&
                typed[1].GetComparison(ListSortDirection.Ascending) is null,
                "A legacy factory without comparison acquired an unrelated sort policy.");
            VerifyPair(facade, raw, row);
            VerifyPair(typed[1], explicitColumn, row);
            Check(raw.Creates == 2 && explicitColumn.Creates == 2 &&
                ReferenceEquals(raw.LastRow, row) && ReferenceEquals(explicitColumn.LastRow, row),
                "Typed creation bypassed an explicit legacy factory or copied its Core row.");
            columns.RemoveAt(0);
            Check(raw.Subscriptions == 1 && ReferenceEquals(typed[1], facade),
                "Removing one duplicate detached or replaced a still-present factory.");
        }
        finally { columns.Clear(); }
        Check(raw.Subscriptions == 0, "Clearing a typed projection retained the borrowed legacy column.");
        Console.WriteLine("UNO_RUNTIME_LEGACY_TYPED_COLUMN_PROJECTION_PASSED: exact explicit factories, shared Core rows, legacy-only facade identity, duplicate subscription ownership and independent native value cleanup");
    }

    // No new typed interface is implemented here: this models an existing
    // application factory rather than changing the fixture to suit the new API.
    private sealed class LegacyOnlyColumn<TModel>(ICellColumn<TModel> inner) : ICellColumn<TModel>
    {
        internal int Creates;
        internal int Subscriptions;
        internal IRow<TModel>? LastRow;
        public double ActualWidth => inner.ActualWidth;
        public bool? CanUserResize => inner.CanUserResize;
        public object? Header => inner.Header;
        public GridLength Width => inner.Width;
        public ListSortDirection? SortDirection { get => inner.SortDirection; set => inner.SortDirection = value; }
        public object? Tag { get => inner.Tag; set => inner.Tag = value; }
        public double MinActualWidth => inner.MinActualWidth;
        public double MaxActualWidth => inner.MaxActualWidth;
        public bool StarWidthWasConstrained => inner.StarWidthWasConstrained;
        public double CellMeasured(double width, int rowIndex) => inner.CellMeasured(width, rowIndex);
        public void CalculateStarWidth(double availableWidth, double totalStars) => inner.CalculateStarWidth(availableWidth, totalStars);
        public bool CommitActualWidth() => inner.CommitActualWidth();
        public void SetWidth(GridLength width) => inner.SetWidth(width);
        U.ICell ICellColumn<TModel>.CreateCell(IRow<TModel> row)
        { ++Creates; LastRow = row; return inner.CreateCell(row); }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { ++Subscriptions; inner.PropertyChanged += value; }
            remove { --Subscriptions; inner.PropertyChanged -= value; }
        }
    }

    private sealed class ExplicitFactoryColumn<TModel> : CellColumnBase<TModel>, ICellColumn<TModel>
    {
        private readonly ICellColumn<TModel> _inner;
        internal int Creates;
        internal IRow<TModel>? LastRow;
        internal ExplicitFactoryColumn(ICellColumn<TModel> inner) : base(inner.Header, inner.Width, new()) => _inner = inner;
        public override U.ICell CreateCell(IRow<TModel> row) =>
            throw new InvalidOperationException("The typed contract bypassed the explicit legacy factory.");
        U.ICell ICellColumn<TModel>.CreateCell(IRow<TModel> row)
        { ++Creates; LastRow = row; return _inner.CreateCell(row); }
    }
}
