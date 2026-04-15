using System;

namespace Avalonia.Controls.Selection
{
    /// <summary>
    /// Compatibility shim for older Uno cell selection event usages.
    /// </summary>
    public class TreeDataGridCellSelectionChangedEventArgs : TreeDataGridSelectionChangedEventArgs
    {
    }

    /// <summary>
    /// Compatibility shim for older Uno cell selection event usages.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    public class TreeDataGridCellSelectionChangedEventArgs<T> : TreeDataGridSelectionChangedEventArgs<T>
        where T : class
    {
    }
}
