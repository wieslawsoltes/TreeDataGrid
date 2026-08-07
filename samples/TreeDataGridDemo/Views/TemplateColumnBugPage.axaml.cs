using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TreeDataGridDemo.Views
{
    public partial class TemplateColumnBugPage : UserControl
    {
        public TemplateColumnBugPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
