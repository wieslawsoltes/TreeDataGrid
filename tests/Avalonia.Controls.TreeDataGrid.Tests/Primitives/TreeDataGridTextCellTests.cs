using System;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Fluent2;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.Primitives;

public class TreeDataGridTextCellTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editing_Shows_Validation_Error_In_Tooltip(bool useFluent2)
    {
        var styles = Application.Current!.Styles;
        IStyle theme = useFluent2 ? new Fluent2Theme() : new FluentTheme();
        var gridStyles = Assert.IsType<Styles>(AvaloniaXamlLoader.Load(
            new Uri("avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml")));
        styles.Add(theme);
        styles.Add(gridStyles);

        var cell = new TreeDataGridTextCell { Value = "Item 0" };
        var window = new Window { Width = 320, Height = 180 };

        try
        {
            window.Show();
            window.Content = cell;
            cell.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(cell.IsEditing);
            Assert.NotNull(cell.Template);
            var editor = Assert.Single(cell.GetVisualDescendants().OfType<TextBox>());

            DataValidationErrors.SetError(editor, new InvalidOperationException("Invalid title"));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var errors = Assert.Single(editor.GetVisualDescendants().OfType<DataValidationErrors>());

            Assert.Contains(errors.GetVisualDescendants().OfType<Control>(),
                control => control.IsEffectivelyVisible && ToolTip.GetTip(control) is not null);
        }
        finally
        {
            window.Close();
            styles.Remove(gridStyles);
            styles.Remove(theme);
        }
    }
}
