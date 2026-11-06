using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample.Demo.Views
{
    public sealed partial class TemplateColumnBugPage : UserControl
    {
        // TemplateColumnItem has required members, so it cannot be [Bindable]. Keep the
        // properties its cell templates bind to in trimmed browser builds.
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(TemplateColumnItem))]
        public TemplateColumnBugPage()
        {
            InitializeComponent();
        }
    }
}
