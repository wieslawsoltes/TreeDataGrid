using IExpanderCell = Avalonia.Controls.Models.TreeDataGrid.IExpanderCellPresentation;
using IRow = global::TreeDataGridCore.Models.IRow;
using System;
using Avalonia.Controls.Models.TreeDataGrid;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridElementFactory
    {
        private TreeDataGridElementPool<string>? _recyclePool;
        private TreeDataGridElementPool<string> RecyclePool => _recyclePool ??= new();

        public virtual Control GetOrCreateElement(object? data, Control parent)
        {
            return RecyclePool.TryTake(GetDataRecycleKey(data), parent, out var element)
                ? element : CreateElement(data);
        }

        public virtual void RecycleElement(Control element)
        {
            RecyclePool.Add(GetElementRecycleKey(element), element);
        }

        /// <summary>
        /// Determines whether an existing element can be reused for the supplied data without
        /// returning it to this factory's recycle pool.
        /// </summary>
        /// <remarks>
        /// This is useful for controls which retain a realized child in the visual tree while
        /// changing its model, avoiding a detach and subsequent style reapplication.
        /// </remarks>
        public virtual bool CanReuseElement(Control element, object? data)
        {
            return GetElementRecycleKey(element) == GetDataRecycleKey(data);
        }

        protected virtual Control CreateElement(object? data)
        {
            return data switch
            {
                CheckBoxCell => new TreeDataGridCheckBoxCell(),
                TemplateCell => new TreeDataGridTemplateCell(),
                IExpanderCell => new TreeDataGridExpanderCell(),
                ICell => new TreeDataGridTextCell(),
                IColumn => new TreeDataGridColumnHeader(),
                IRow => new TreeDataGridRow(),
                _ => throw new NotSupportedException(),
            };
        }

        protected virtual string GetDataRecycleKey(object? data)
        {
            return data switch
            {
                CheckBoxCell => typeof(TreeDataGridCheckBoxCell).FullName!,
                TemplateCell => typeof(TreeDataGridTemplateCell).FullName!,
                IExpanderCell => typeof(TreeDataGridExpanderCell).FullName!,
                ICell => typeof(TreeDataGridTextCell).FullName!,
                IColumn => typeof(TreeDataGridColumnHeader).FullName!,
                IRow => typeof(TreeDataGridRow).FullName!,
                _ => throw new NotSupportedException(),
            };
        }

        protected virtual string GetElementRecycleKey(Control element)
        {
            return element.GetType().FullName!;
        }

    }
}
