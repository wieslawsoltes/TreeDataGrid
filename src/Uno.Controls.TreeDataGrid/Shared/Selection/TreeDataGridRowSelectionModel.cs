using System.Collections;
using System.Collections.Generic;

namespace Avalonia.Controls.Selection
{
    public partial class TreeDataGridRowSelectionModel<TModel> : TreeSelectionModelBase<TModel>,
        ITreeDataGridRowSelectionModel<TModel>
        where TModel : class
    {
        protected readonly ITreeDataGridSource<TModel> _source;

        public TreeDataGridRowSelectionModel(ITreeDataGridSource<TModel> source)
            : base(source.Items)
        {
            _source = source;
            OnConstructed();
        }

        IEnumerable? ITreeDataGridSelection.Source
        {
            get => Source;
            set => Source = value;
        }

        protected internal override IEnumerable<TModel>? GetChildren(TModel node)
        {
            if (_source is HierarchicalTreeDataGridSource<TModel> treeSource)
                return treeSource.GetModelChildren(node);

            return null;
        }

        partial void OnConstructed();
    }
}
