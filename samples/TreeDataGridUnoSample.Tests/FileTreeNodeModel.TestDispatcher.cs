using System;

namespace TreeDataGridDemo.Models;

public partial class FileTreeNodeModel
{
    private static partial Action<Action> CreateDispatcher() => throw new InvalidOperationException("Tests must supply a dispatcher.");
}
