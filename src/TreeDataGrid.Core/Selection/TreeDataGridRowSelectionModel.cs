using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
namespace TreeDataGridCore.Selection
{
    public class TreeDataGridRowSelectionModel<TModel> : TreeSelectionModelBase<TModel>, ITreeDataGridRowSelectionModel<TModel> where TModel : class
    {
        private readonly ITreeDataGridSource<TModel> _source;
        private bool _stateChanged;
        public event EventHandler? StateChanged;
        public TreeDataGridRowSelectionModel(ITreeDataGridSource<TModel> source) : base(source.Items)
        {
            _source = source;
            SelectionChanged += (_, _) => NotifyStateChanged();
            IndexesChanged += (_, _) => NotifyStateChanged();
            SourceReset += (_, _) => NotifyStateChanged();
        }
        private void NotifyStateChanged()
        {
            if (IsSourceCollectionChanging)
                _stateChanged = true;
            else
                StateChanged?.Invoke(this, EventArgs.Empty);
        }
        protected override void OnSourceCollectionChangeFinished()
        {
            if (!_stateChanged)
                return;
            _stateChanged = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        IEnumerable? ITreeDataGridSelection.Source { get => Source; set => Source = value; }
        protected internal override IEnumerable<TModel>? GetChildren(TModel node) => _source.GetModelChildren(node)?.Cast<TModel>();
    }
}
