using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Selection
{
    public partial class TreeDataGridRowSelectionModel<TModel>
        where TModel : class
    {
        private int _lastCharPressedTime;
        private string _typedWord = string.Empty;

        protected void HandleTextInput(string? text, TreeDataGrid treeDataGrid, int selectedRowIndex)
        {
            if (text != null && treeDataGrid.Columns != null)
            {
                var typedChar = text.ToUpper()[0];

                int now = Environment.TickCount;
                int time = 0;
                if (_lastCharPressedTime > 0)
                {
                    time = now - _lastCharPressedTime;
                }

                string candidatePattern;
                if (time < 500)
                {
                    if (_typedWord.Length == 1 && typedChar == _typedWord[0])
                    {
                        candidatePattern = _typedWord;
                    }
                    else
                    {
                        candidatePattern = _typedWord + typedChar;
                    }
                }
                else
                {
                    candidatePattern = typedChar.ToString();
                }
                foreach (var column in treeDataGrid.Columns)
                {
                    if (column is ITextSearchableColumn<TModel> textSearchableColumn && textSearchableColumn.IsTextSearchEnabled)
                    {
                        Search(treeDataGrid, candidatePattern, selectedRowIndex, textSearchableColumn);
                    }
                    else if (column is HierarchicalExpanderColumn<TModel> hierarchicalColumn &&
                        hierarchicalColumn.Inner is ITextSearchableColumn<TModel> textSearchableColumn2 &&
                        textSearchableColumn2.IsTextSearchEnabled)
                    {
                        Search(treeDataGrid, candidatePattern, selectedRowIndex, textSearchableColumn2);
                    }

                }
                _lastCharPressedTime = now;
            }
        }

        private void Search(TreeDataGrid treeDataGrid, string candidatePattern, int selectedRowIndex, ITextSearchableColumn<TModel> column)
        {
            var found = false;
            for (int i = candidatePattern.Length == 1 ? selectedRowIndex + 1 : selectedRowIndex; i <= _source.Rows.Count - 1; i++)
            {
                found = SearchAndSelectRow(treeDataGrid, candidatePattern, i, (TModel?)_source.Rows[i].Model, column.SelectValue);
                if (found)
                {
                    break;
                }
            }
            if (!found)
            {
                for (int i = 0; i <= selectedRowIndex; i++)
                {
                    found = SearchAndSelectRow(treeDataGrid, candidatePattern, i, (TModel?)_source.Rows[i].Model, column.SelectValue);
                    if (found)
                    {
                        break;
                    }
                }
            }
        }

        private bool SearchAndSelectRow(
            TreeDataGrid treeDataGrid,
            string candidatePattern,
            int newIndex,
            TModel? model,
            Func<TModel, string?>? valueSelector)
        {
            if (valueSelector != null && model != null)
            {
                var value = valueSelector(model);
                if (value != null && value.ToUpper().StartsWith(candidatePattern))
                {
                    UpdateSelection(treeDataGrid, newIndex);
                    _ = BringRowIntoView(treeDataGrid, newIndex);
                    _typedWord = candidatePattern;
                    return true;
                }
            }

            return false;
        }

        void ITreeDataGridSelectionInteraction.OnTextInput(TreeDataGrid sender, TextInputEventArgs e)
        {
            HandleTextInput(e.Text, sender, _source.Rows.ModelIndexToRowIndex(AnchorIndex));
        }

        private static partial IReadOnlyList<TreeDataGridRow> GetRealizedRows(TreeDataGrid sender)
        {
            return sender.RowsPresenter?.GetRealizedElements().OfType<TreeDataGridRow>().ToArray()
                ?? Array.Empty<TreeDataGridRow>();
        }

        private static partial bool IsRowFullyVisibleToUser(TreeDataGrid sender, TreeDataGridRow row)
        {
            var scrollContentPresenter = row.FindAncestorOfType<ScrollContentPresenter>();
            if (scrollContentPresenter is null)
                return false;

            var transform = row.TransformToVisual(scrollContentPresenter);
            if (transform is null)
                return false;

            var transformedBounds = new Rect(row.Bounds.Size).TransformToAABB((Matrix)transform);
            return scrollContentPresenter.Bounds.Contains(transformedBounds.TopLeft) &&
                scrollContentPresenter.Bounds.Contains(transformedBounds.BottomRight);
        }

        private static partial bool GetToggleModifier(TreeDataGrid sender, PointerEventArgs e)
        {
            var commandModifiers = sender.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers;
            return commandModifiers is not null && e.KeyModifiers.HasFlag(commandModifiers);
        }

        private static partial void FocusRow(TreeDataGrid owner, TreeDataGridRow? row)
        {
            if (row?.CellsPresenter is null)
                return;

            if (TopLevel.GetTopLevel(owner)?.FocusManager is { } focusManager &&
                focusManager.GetFocusedElement() is Control currentFocus &&
                owner.TryGetCell(currentFocus, out var currentCell) &&
                row.TryGetCell(currentCell.ColumnIndex) is { } newCell &&
                newCell.Focusable)
            {
                newCell.Focus();
                return;
            }

            foreach (var cell in row.CellsPresenter.GetRealizedElements())
            {
                if (cell.Focusable)
                {
                    cell.Focus();
                    break;
                }
            }
        }
    }
}
