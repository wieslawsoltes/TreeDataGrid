using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using TreeDataGridDemo.Models;
using Uno.Controls;

namespace TreeDataGridUnoSample;

internal static class SharedSampleBindings
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // A genuinely empty collection has no sample object from which to infer
        // the model type. Preserve that contract without enumerating interfaces.
        TreeDataGridBindingRegistry.RegisterCollection<ObservableCollection<Person>, Person>();
        TreeDataGridBindingRegistry.RegisterCollection<ObservableCollection<DragDropItem>, DragDropItem>();
        TreeDataGridBindingRegistry.RegisterCollection<ObservableCollection<FileTreeNodeModel>, FileTreeNodeModel>();
    }
}
