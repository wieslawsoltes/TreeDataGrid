using Microsoft.UI.Xaml.Data;

// Uno generates trim-safe binding accessors for public [Bindable] types only.
// Internal demo view models are preserved with DynamicDependency in MainWindow.
namespace TreeDataGridUnoSample.Demo.ViewModels
{
    [Bindable]
    public partial class FilesPageViewModel { }
}

namespace TreeDataGridUnoSample.Demo
{
    [Bindable]
    public sealed partial class CountryRegions { }
}
