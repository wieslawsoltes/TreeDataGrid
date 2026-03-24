using System;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace Avalonia.Controls
{
    public class TreeDataGridRowDragEventArgs : EventArgs
    {
        private DragDropEffects _dragEffects;

        public TreeDataGridRowDragEventArgs(TreeDataGridRow? row, DragEventArgs inner)
        {
            TargetRow = row;
            Inner = inner;
            _dragEffects = FromDataPackageOperation(inner.AcceptedOperation);
        }

        public DragEventArgs Inner { get; }
        public TreeDataGridRow? TargetRow { get; }
        public TreeDataGridRowDropPosition Position { get; set; }
        public bool Handled { get; set; }

        public DragDropEffects DragEffects
        {
            get => _dragEffects;
            set
            {
                _dragEffects = value;
                Inner.AcceptedOperation = ToDataPackageOperation(value);
            }
        }

        private static DragDropEffects FromDataPackageOperation(DataPackageOperation operation)
        {
            var result = DragDropEffects.None;

            if (operation.HasFlag(DataPackageOperation.Copy))
                result |= DragDropEffects.Copy;
            if (operation.HasFlag(DataPackageOperation.Move))
                result |= DragDropEffects.Move;
            if (operation.HasFlag(DataPackageOperation.Link))
                result |= DragDropEffects.Link;

            return result;
        }

        private static DataPackageOperation ToDataPackageOperation(DragDropEffects effects)
        {
            var result = DataPackageOperation.None;

            if (effects.HasFlag(DragDropEffects.Copy))
                result |= DataPackageOperation.Copy;
            if (effects.HasFlag(DragDropEffects.Move))
                result |= DataPackageOperation.Move;
            if (effects.HasFlag(DragDropEffects.Link))
                result |= DataPackageOperation.Link;

            return result;
        }
    }
}
