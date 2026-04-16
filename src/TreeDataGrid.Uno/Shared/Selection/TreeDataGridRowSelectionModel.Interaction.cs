using System;
using System.Collections.Generic;
using Uno.Controls.Primitives;
using Avalonia.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRect = Windows.Foundation.Rect;

namespace Uno.Controls.Selection
{
    public partial class TreeDataGridRowSelectionModel<TModel>
        where TModel : class
    {
        private static partial IReadOnlyList<TreeDataGridRow> GetRealizedRows(TreeDataGrid sender)
        {
            return sender.RowsPresenter?.RealizedRows ?? Array.Empty<TreeDataGridRow>();
        }

        private static partial bool IsRowFullyVisibleToUser(TreeDataGrid sender, TreeDataGridRow row)
        {
            if (sender.Scroll is null || sender.RowsPresenter is null || row.ActualHeight <= 0)
                return false;

            var transform = row.TransformToVisual(sender.RowsPresenter);
            var bounds = transform.TransformBounds(new WinRect(0, 0, row.ActualWidth, row.ActualHeight));
            var viewportTop = sender.Scroll.VerticalOffset;
            var viewportBottom = viewportTop + sender.Scroll.ViewportHeight;

            return bounds.Top >= viewportTop && bounds.Bottom <= viewportBottom;
        }

        private static partial bool GetToggleModifier(TreeDataGrid sender, PointerEventArgs e)
        {
            _ = sender;
            return e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        }

        private static partial void FocusRow(TreeDataGrid owner, TreeDataGridRow? row)
        {
            if (row?.CellsPresenter is null)
                return;

            if (owner.XamlRoot is not null &&
                Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(owner.XamlRoot) is Control currentFocus &&
                owner.TryGetCell(currentFocus, out var currentCell) &&
                row.TryGetCell(currentCell.ColumnIndex) is { } newCell &&
                newCell.IsTabStop)
            {
                newCell.Focus(FocusState.Programmatic);
                return;
            }

            foreach (var cell in row.CellsPresenter.RealizedCells)
            {
                if (!cell.IsTabStop)
                    continue;

                cell.Focus(FocusState.Programmatic);
                break;
            }
        }
    }
}
