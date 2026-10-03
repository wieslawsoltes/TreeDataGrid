using Microsoft.UI.Xaml.Data;

// Attributes belong to the Uno frontend, not to the shared Core or Avalonia
// sample files. Uno emits direct binding accessors for these model properties.
// The generator only handles public types; internal types fall back to
// reflection, which trimmed browser builds do not preserve.
namespace TreeDataGridDemo.Models;

[Bindable]
public partial class Person { }

[Bindable]
public partial class DragDropItem { }

[Bindable]
public partial class FileTreeNodeModel { }

[Bindable]
public partial class Country { }
