using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TreeDataGridTemplateCellTests
    {
        [AvaloniaFact]
        public void Recycled_Cell_Uses_New_Columns_Template_When_Realized_While_Detached()
        {
            var firstTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock());
            var secondTemplate = new FuncDataTemplate<object>((_, _) => new Border());
            var firstEditingTemplate = new FuncDataTemplate<object>((_, _) => new TextBox());
            var secondEditingTemplate = new FuncDataTemplate<object>((_, _) => new CheckBox());
            var firstModel = new TemplateCell(
                new object(),
                _ => firstTemplate,
                _ => firstEditingTemplate,
                null);
            var secondModel = new TemplateCell(
                new object(),
                _ => secondTemplate,
                _ => secondEditingTemplate,
                null);
            var factory = new TreeDataGridElementFactory();
            var cell = new TreeDataGridTemplateCell();
            var panel = new StackPanel();
            var window = new Window { Content = panel };

            try
            {
                cell.Realize(factory, null, firstModel, 0, 0);
                panel.Children.Add(cell);
                window.Show();

                Assert.IsType<TextBlock>(cell.ContentTemplate!.Build(firstModel.Value));
                Assert.IsType<TextBox>(cell.EditingTemplate!.Build(firstModel.Value));

                panel.Children.Remove(cell);
                cell.Unrealize();
                cell.Realize(factory, null, secondModel, 3, 0);
                panel.Children.Add(cell);

                Assert.IsType<Border>(cell.ContentTemplate!.Build(secondModel.Value));
                Assert.IsType<CheckBox>(cell.EditingTemplate!.Build(secondModel.Value));
            }
            finally
            {
                window.Close();
            }
        }
    }
}
