using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridDemo.Models;
using Uno.Controls.Models.TreeDataGrid;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;

namespace TreeDataGridUnoSample.Demo.ViewModels
{
    internal class DragDropPageViewModel
    {
        private ObservableCollection<DragDropItem> _data;

        public DragDropPageViewModel()
        {
            _data = DragDropItem.CreateRandomItems();
            var source = new HierarchicalTreeDataGridSource<DragDropItem>(_data)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<DragDropItem>(
                        new TextColumn<DragDropItem, string>(
                            "Name",
                            x => x.Name,
                            new GridLength(1, GridUnitType.Star)),
                        x => x.Children),
                    new CheckBoxColumn<DragDropItem>(
                        "Allow Drag",
                        x => x.AllowDrag,
                        (o, x) => o.AllowDrag = x),
                    new CheckBoxColumn<DragDropItem>(
                        "Allow Drop",
                        x => x.AllowDrop,
                        (o, x) => o.AllowDrop = x),
                }
            };

            source.RowSelection!.SingleSelect = false;
            Source = source;
        }

        public ITreeDataGridSource<DragDropItem> Source { get; }
    }
}
