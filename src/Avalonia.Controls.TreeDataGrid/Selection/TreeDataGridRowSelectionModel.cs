using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Selection
{
    public class TreeDataGridRowSelectionModel<TModel> : TreeSelectionModelBase<TModel>,
        ITreeDataGridRowSelectionModel<TModel>,
        ITreeDataGridSelectionInteraction
        where TModel : class
    {
        private readonly ITreeDataGridSource<TModel> _source;
        private readonly TreeDataGridRowSelectionInteraction<TModel> _interaction;
        private EventHandler? _viewSelectionChanged;
        private bool _raiseViewSelectionChanged;

        public TreeDataGridRowSelectionModel(ITreeDataGridSource<TModel> source)
            : base(source.Items)
        {
            _source = source;
            _interaction = new(source, this);
            SelectionChanged += (s, e) =>
            {
                if (!IsSourceCollectionChanging)
                    _viewSelectionChanged?.Invoke(this, e);
                else
                    _raiseViewSelectionChanged = true;
            };
        }

        event EventHandler? ITreeDataGridSelectionInteraction.SelectionChanged
        {
            add => _viewSelectionChanged += value;
            remove => _viewSelectionChanged -= value;
        }
        IEnumerable? ITreeDataGridSelection.Source { get => Source; set => Source = value; }
        protected void HandleTextInput(string? text, TreeDataGrid treeDataGrid, int selectedRowIndex)
            => _interaction.HandleTextInput(text, treeDataGrid, selectedRowIndex);
        bool ITreeDataGridSelectionInteraction.IsRowSelected(IRow rowModel) => _interaction.IsRowSelected(rowModel);
        bool ITreeDataGridSelectionInteraction.IsRowSelected(int rowIndex) => _interaction.IsRowSelected(rowIndex);
        void ITreeDataGridSelectionInteraction.OnKeyDown(TreeDataGrid sender, KeyEventArgs e) => _interaction.OnKeyDown(sender, e);
        void ITreeDataGridSelectionInteraction.OnPreviewKeyDown(TreeDataGrid sender, KeyEventArgs e) => _interaction.OnPreviewKeyDown(sender, e);
        void ITreeDataGridSelectionInteraction.OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e) => _interaction.OnPointerPressed(sender, e);
        void ITreeDataGridSelectionInteraction.OnPointerReleased(TreeDataGrid sender, PointerReleasedEventArgs e) => _interaction.OnPointerReleased(sender, e);
        void ITreeDataGridSelectionInteraction.OnTextInput(TreeDataGrid sender, TextInputEventArgs e) => _interaction.OnTextInput(sender, e);

        protected internal override IEnumerable<TModel>? GetChildren(TModel node)
        {
            if (_source is HierarchicalTreeDataGridSource<TModel> treeSource)
            {
                return treeSource.GetModelChildren(node);
            }

            return null;
        }

        protected override void OnSourceCollectionChangeFinished()
        {
            if (_raiseViewSelectionChanged)
            {
                _viewSelectionChanged?.Invoke(this, EventArgs.Empty);
                _raiseViewSelectionChanged = false;
            }
        }

    }
}
