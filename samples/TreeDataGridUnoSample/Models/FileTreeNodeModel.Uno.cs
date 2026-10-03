using System;
using Microsoft.UI.Dispatching;

namespace TreeDataGridDemo.Models;

public partial class FileTreeNodeModel
{
    private static partial Action<Action> CreateDispatcher()
    {
        var queue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("Create file nodes on the UI thread or provide a dispatcher.");
        return action => queue.TryEnqueue(() => action());
    }
}
