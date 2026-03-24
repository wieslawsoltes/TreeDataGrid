using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace Avalonia.Controls
{
    public class TreeDataGridRowDragStartedEventArgs : EventArgs
    {
        public TreeDataGridRowDragStartedEventArgs(IEnumerable<object> models)
        {
            Models = models;
        }

        public DragDropEffects AllowedEffects { get; set; }
        public IEnumerable<object> Models { get; }
    }
}
