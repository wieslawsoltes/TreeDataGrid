using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Adapters
{
    internal sealed class CoreRowSelectionAdapter<TModel> : ITreeDataGridRowSelectionModel<TModel>, ITreeDataGridSelectionInteraction, IDisposable where TModel : class
    {
        private readonly TreeDataGridRowSelectionInteraction<TModel> _interaction;
        public Core.Selection.ITreeDataGridRowSelectionModel<TModel> Model { get; }
        public CoreRowSelectionAdapter(ITreeDataGridSource<TModel> source, Core.Selection.ITreeDataGridRowSelectionModel<TModel> model)
        {
            Model = model;
            _interaction = new(source, this);
            Model.PropertyChanged += OnPropertyChanged;
            Model.SelectionChanged += OnSelectionChanged;
            Model.IndexesChanged += OnIndexesChanged;
            Model.SourceReset += OnSourceReset;
            Model.StateChanged += OnStateChanged;
            SelectedIndexes = Model.SelectedIndexes.ToAvalonia();
        }
        public IEnumerable? Source { get => ((Core.Selection.ITreeSelectionModel)Model).Source; set => ((Core.Selection.ITreeSelectionModel)Model).Source = value; }
        public bool SingleSelect { get => Model.SingleSelect; set => Model.SingleSelect = value; }
        public IndexPath SelectedIndex { get => Model.SelectedIndex.ToAvalonia(); set => Model.SelectedIndex = value.ToCore(); }
        public IndexPath AnchorIndex { get => Model.AnchorIndex.ToAvalonia(); set => Model.AnchorIndex = value.ToCore(); }
        public IndexPath RangeAnchorIndex { get => Model.RangeAnchorIndex.ToAvalonia(); set => Model.RangeAnchorIndex = value.ToCore(); }
        public IReadOnlyList<IndexPath> SelectedIndexes { get; }
        public TModel? SelectedItem => Model.SelectedItem;
        object? ITreeSelectionModel.SelectedItem => SelectedItem;
        public IReadOnlyList<TModel?> SelectedItems => Model.SelectedItems;
        IReadOnlyList<object?> ITreeSelectionModel.SelectedItems => SelectedItems;
        public int Count => Model.Count;
        public void Clear() => Model.Clear();
        public void Select(IndexPath index) => Model.Select(index.ToCore());
        public void Deselect(IndexPath index) => Model.Deselect(index.ToCore());
        public bool IsSelected(IndexPath index) => Model.IsSelected(index.ToCore());
        public void BeginBatchUpdate() => Model.BeginBatchUpdate();
        public void EndBatchUpdate() => Model.EndBatchUpdate();
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<TreeDataGridSelectionChangedEventArgs<TModel>>? SelectionChanged;
        private event EventHandler<TreeDataGridSelectionChangedEventArgs>? UntypedSelectionChanged;
        event EventHandler<TreeDataGridSelectionChangedEventArgs>? ITreeSelectionModel.SelectionChanged
        { add => UntypedSelectionChanged += value; remove => UntypedSelectionChanged -= value; }
        private event EventHandler? ViewSelectionChanged;
        event EventHandler? ITreeDataGridSelectionInteraction.SelectionChanged
        { add => ViewSelectionChanged += value; remove => ViewSelectionChanged -= value; }
        public event EventHandler<TreeSelectionModelIndexesChangedEventArgs>? IndexesChanged;
        public event EventHandler<TreeSelectionModelSourceResetEventArgs>? SourceReset;
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, e);
        private void OnSelectionChanged(object? sender, Core.Selection.TreeSelectionModelSelectionChangedEventArgs<TModel> e)
        {
            var args = new TreeDataGridSelectionChangedEventArgs<TModel>(e.DeselectedIndexes.ToAvalonia(), e.SelectedIndexes.ToAvalonia(), e.DeselectedItems, e.SelectedItems);
            SelectionChanged?.Invoke(this, args);
            UntypedSelectionChanged?.Invoke(this, new TreeDataGridSelectionChangedEventArgs(args.DeselectedIndexes, args.SelectedIndexes, args.DeselectedItems, args.SelectedItems));
        }
        private void OnIndexesChanged(object? sender, Core.Selection.TreeSelectionModelIndexesChangedEventArgs e) =>
            IndexesChanged?.Invoke(this, new(e.ParentIndex.ToAvalonia(), e.StartIndex, e.EndIndex, e.Delta));
        private void OnSourceReset(object? sender, Core.Selection.TreeSelectionModelSourceResetEventArgs e) => SourceReset?.Invoke(this, new(e.ParentIndex.ToAvalonia()));
        private void OnStateChanged(object? sender, EventArgs e) => ViewSelectionChanged?.Invoke(this, e);
        public void Dispose()
        {
            Model.PropertyChanged -= OnPropertyChanged;
            Model.SelectionChanged -= OnSelectionChanged;
            Model.IndexesChanged -= OnIndexesChanged;
            Model.SourceReset -= OnSourceReset;
            Model.StateChanged -= OnStateChanged;
        }
        bool ITreeDataGridSelectionInteraction.IsRowSelected(IRow rowModel) => _interaction.IsRowSelected(rowModel);
        bool ITreeDataGridSelectionInteraction.IsRowSelected(int rowIndex) => _interaction.IsRowSelected(rowIndex);
        void ITreeDataGridSelectionInteraction.OnKeyDown(TreeDataGrid sender, KeyEventArgs e) => _interaction.OnKeyDown(sender, e);
        void ITreeDataGridSelectionInteraction.OnPreviewKeyDown(TreeDataGrid sender, KeyEventArgs e) => _interaction.OnPreviewKeyDown(sender, e);
        void ITreeDataGridSelectionInteraction.OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e) => _interaction.OnPointerPressed(sender, e);
        void ITreeDataGridSelectionInteraction.OnPointerReleased(TreeDataGrid sender, PointerReleasedEventArgs e) => _interaction.OnPointerReleased(sender, e);
        void ITreeDataGridSelectionInteraction.OnTextInput(TreeDataGrid sender, TextInputEventArgs e) => _interaction.OnTextInput(sender, e);
    }
}
