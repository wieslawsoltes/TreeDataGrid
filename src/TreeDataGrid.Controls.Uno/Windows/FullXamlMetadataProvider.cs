#if WINDOWS
// Applications consult this library's generated XAML metadata provider for its types. Without
// [FullXamlMetadataProvider] they also generate their own entries for library types they reach
// (property and base types), and those entries lack members: for example the members of the
// generic presenter bases that the theme binds ("Failed to assign to property
// ...TreeDataGridPresenterBase`1<...>.ElementFactory"). With it, applications defer every type of
// this assembly to the provider below, whose metadata XamlMetadata.xaml makes complete.
#if TREEDATAGRID_WINUI_PACKAGE
namespace Uno.Controls.TreeDataGrid_Controls_WinUI_XamlTypeInfo;
#else
namespace Uno.Controls.TreeDataGrid_Controls_Uno_XamlTypeInfo;
#endif

[Microsoft.UI.Xaml.Markup.FullXamlMetadataProvider]
public sealed partial class XamlMetaDataProvider;
#endif
