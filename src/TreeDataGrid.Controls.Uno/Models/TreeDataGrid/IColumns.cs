using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>View-owned column measurement and committed layout over shared Core definitions.</summary>
public interface IColumns : IReadOnlyList<IColumn>, INotifyCollectionChanged
{
    event EventHandler? LayoutInvalidated;
    Size CellMeasured(int columnIndex, int rowIndex, Size size);
    (int index, double x) GetColumnAt(double x);
    double GetEstimatedWidth(double constraint);
    void CommitActualWidths();
    void SetColumnWidth(int columnIndex, GridLength width);
    void ViewportChanged(Rect viewport);
}

internal interface IColumnLayoutBatch
{
    bool IsActualWidthCommitDeferred { get; }
    void BeginActualWidthBatch();
    bool EndActualWidthBatch();
    void RequestFinalMeasure();
}
