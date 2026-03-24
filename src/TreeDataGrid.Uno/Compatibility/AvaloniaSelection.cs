using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Controls.Selection
{
    public interface ISelectionModel
    {
        int Count { get; }
        int SelectedIndex { get; set; }
        int AnchorIndex { get; set; }
        bool SingleSelect { get; set; }
        IReadOnlyList<int> SelectedIndexes { get; }
        IReadOnlyList<object?> SelectedItems { get; }
        object? SelectedItem { get; set; }

        event EventHandler<SelectionModelSelectionChangedEventArgs>? SelectionChanged;

        bool IsSelected(int index);
        void Select(int index);
        void SelectRange(int startIndex, int endIndex);
        void Clear();
        void BeginBatchUpdate();
        void EndBatchUpdate();
    }

    public class SelectionModelSelectionChangedEventArgs : EventArgs
    {
    }

    public class SelectionModel<T> : ISelectionModel
    {
        private readonly IReadOnlyList<T> _items;
        private readonly SortedSet<int> _selectedIndexes = new();
        private int _batchDepth;
        private bool _hasPendingChanges;

        public SelectionModel(IReadOnlyList<T> items)
        {
            _items = items;
        }

        public int Count => _selectedIndexes.Count;

        public int SelectedIndex
        {
            get => _selectedIndexes.Count > 0 ? _selectedIndexes.Min : -1;
            set
            {
                if (value < 0)
                {
                    Clear();
                    return;
                }

                if (SingleSelect &&
                    _selectedIndexes.Count == 1 &&
                    _selectedIndexes.Contains(value))
                {
                    AnchorIndex = value;
                    return;
                }

                _selectedIndexes.Clear();
                _selectedIndexes.Add(value);
                AnchorIndex = value;
                RaiseSelectionChanged();
            }
        }

        public int AnchorIndex { get; set; } = -1;

        public bool SingleSelect { get; set; } = true;

        public IReadOnlyList<int> SelectedIndexes => _selectedIndexes.ToList();

        public IReadOnlyList<T?> SelectedItems =>
            _selectedIndexes
                .Where(x => x >= 0 && x < _items.Count)
                .Select(x => (T?)_items[x])
                .ToList();

        IReadOnlyList<object?> ISelectionModel.SelectedItems =>
            SelectedItems.Cast<object?>().ToList();

        public T? SelectedItem
        {
            get => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : default;
            set
            {
                if (value is null)
                {
                    Clear();
                    return;
                }

                var comparer = EqualityComparer<T>.Default;
                for (var i = 0; i < _items.Count; ++i)
                {
                    if (comparer.Equals(_items[i], value))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }

                Clear();
            }
        }

        object? ISelectionModel.SelectedItem
        {
            get => SelectedItem;
            set
            {
                if (value is null)
                {
                    Clear();
                }
                else if (value is T typed)
                {
                    SelectedItem = typed;
                }
            }
        }

        public event EventHandler<SelectionModelSelectionChangedEventArgs>? SelectionChanged;

        public void BeginBatchUpdate()
        {
            ++_batchDepth;
        }

        public void EndBatchUpdate()
        {
            if (_batchDepth == 0)
                return;

            --_batchDepth;
            if (_batchDepth == 0 && _hasPendingChanges)
            {
                _hasPendingChanges = false;
                SelectionChanged?.Invoke(this, new SelectionModelSelectionChangedEventArgs());
            }
        }

        public void Clear()
        {
            if (_selectedIndexes.Count == 0)
                return;

            _selectedIndexes.Clear();
            AnchorIndex = -1;
            RaiseSelectionChanged();
        }

        public bool IsSelected(int index) => _selectedIndexes.Contains(index);

        public void Select(int index)
        {
            if (index < 0 || index >= _items.Count)
                return;

            if (SingleSelect)
            {
                SelectedIndex = index;
                return;
            }

            if (_selectedIndexes.Add(index))
            {
                if (AnchorIndex < 0)
                    AnchorIndex = index;
                RaiseSelectionChanged();
            }
        }

        public void SelectRange(int startIndex, int endIndex)
        {
            if (_items.Count == 0)
                return;

            var start = Math.Clamp(Math.Min(startIndex, endIndex), 0, _items.Count - 1);
            var end = Math.Clamp(Math.Max(startIndex, endIndex), 0, _items.Count - 1);

            if (SingleSelect)
            {
                SelectedIndex = end;
                return;
            }

            var changed = false;
            for (var i = start; i <= end; ++i)
                changed |= _selectedIndexes.Add(i);

            if (changed)
            {
                if (AnchorIndex < 0)
                    AnchorIndex = start;
                RaiseSelectionChanged();
            }
        }

        private void RaiseSelectionChanged()
        {
            if (_batchDepth > 0)
            {
                _hasPendingChanges = true;
            }
            else
            {
                SelectionChanged?.Invoke(this, new SelectionModelSelectionChangedEventArgs());
            }
        }
    }
}
