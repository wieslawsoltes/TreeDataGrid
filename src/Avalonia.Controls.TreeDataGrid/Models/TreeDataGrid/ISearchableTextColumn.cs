#if TREE_DATAGRID_UNO
namespace Uno.Controls.Models.TreeDataGrid
#else
namespace Avalonia.Controls.Models.TreeDataGrid
#endif
{
    public interface ITextSearchableColumn<TModel>
    {
        public bool IsTextSearchEnabled { get; }
        internal string? SelectValue(TModel model);
    }
}
