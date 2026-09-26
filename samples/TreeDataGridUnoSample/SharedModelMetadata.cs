using Microsoft.UI.Xaml.Data;

// Attributes belong to the Uno frontend, not to the shared Core or Avalonia
// sample files. Uno emits direct binding accessors for these model properties.
namespace TreeDataGridDemo.Models;

[Bindable]
internal partial class Person { }

[Bindable]
public partial class DragDropItem { }

[Bindable]
public partial class FileTreeNodeModel { }
