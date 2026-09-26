using System;
using Avalonia.Threading;

namespace TreeDataGridDemo.Models;

public partial class FileTreeNodeModel
{
    private static partial Action<Action> CreateDispatcher() => action => Dispatcher.UIThread.Post(action);
}
