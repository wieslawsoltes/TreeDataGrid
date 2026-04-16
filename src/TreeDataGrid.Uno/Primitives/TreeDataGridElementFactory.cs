using System;
using System.Collections.Generic;
using Uno.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives
{
    public class TreeDataGridElementFactory
    {
        private readonly Dictionary<string, List<FrameworkElement>> _recyclePool = new();

        public FrameworkElement GetOrCreateElement(object data)
        {
            var recycleKey = GetDataRecycleKey(data);

            if (_recyclePool.TryGetValue(recycleKey, out var elements) && elements.Count > 0)
            {
                var element = elements[^1];
                elements.RemoveAt(elements.Count - 1);

                if (element.Parent is Panel parent)
                    parent.Children.Remove(element);

                return element;
            }

            return CreateElement(data);
        }

        public void RecycleElement(FrameworkElement element)
        {
            var recycleKey = GetElementRecycleKey(element);

            if (!_recyclePool.TryGetValue(recycleKey, out var elements))
            {
                elements = new List<FrameworkElement>();
                _recyclePool.Add(recycleKey, elements);
            }

            if (element.Parent is Panel parent)
                parent.Children.Remove(element);

            elements.Add(element);
        }

        protected virtual FrameworkElement CreateElement(object data)
        {
            return data switch
            {
                CheckBoxCell => new TreeDataGridCheckBoxCell(),
                TemplateCell => new TreeDataGridTemplateCell(),
                IExpanderCell => new TreeDataGridExpanderCell(),
                ITextCell => new TreeDataGridTextCell(),
                ICell => new TreeDataGridTextCell(),
                IColumn => new TreeDataGridColumnHeader(),
                IRow => new TreeDataGridRow(),
                _ => throw new NotSupportedException($"Unsupported TreeDataGrid element model: {data.GetType().FullName}"),
            };
        }

        protected virtual string GetDataRecycleKey(object data)
        {
            return data switch
            {
                CheckBoxCell => typeof(TreeDataGridCheckBoxCell).FullName!,
                TemplateCell => typeof(TreeDataGridTemplateCell).FullName!,
                IExpanderCell => typeof(TreeDataGridExpanderCell).FullName!,
                ITextCell => typeof(TreeDataGridTextCell).FullName!,
                ICell => typeof(TreeDataGridTextCell).FullName!,
                IColumn => typeof(TreeDataGridColumnHeader).FullName!,
                IRow => typeof(TreeDataGridRow).FullName!,
                _ => throw new NotSupportedException($"Unsupported TreeDataGrid element model: {data.GetType().FullName}"),
            };
        }

        protected virtual string GetElementRecycleKey(FrameworkElement element)
        {
            return element.GetType().FullName!;
        }
    }
}
