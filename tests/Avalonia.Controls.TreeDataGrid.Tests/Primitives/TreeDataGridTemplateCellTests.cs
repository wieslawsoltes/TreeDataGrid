using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

                Assert.Null(cell.Content);

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

        [AvaloniaFact]
        public void Retained_Unrealized_Cell_Keeps_Its_Templated_Control_Parented()
        {
            var template = new TrackingDataTemplate();
            var firstModel = new TemplateCell(new object(), _ => template, null, null);
            var secondModel = new TemplateCell(new object(), _ => template, null, null);
            var factory = new TreeDataGridElementFactory();
            var cell = new TreeDataGridTemplateCell
            {
                Template = TestTemplates.TreeDataGridTemplateCellTemplate(),
            };
            var panel = new StackPanel { Children = { cell } };
            var window = new TestWindow(panel);

            try
            {
                cell.Realize(factory, null, firstModel, 0, 0);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                var content = Assert.IsType<TrackingControl>(
                    cell.GetVisualDescendants().Single(x => x is TrackingControl));
                var logicalParent = content.Parent;
                var visualParent = content.GetVisualParent();
                var logicalAttaches = 0;
                var logicalDetaches = 0;
                var visualAttaches = 0;
                var visualDetaches = 0;

                content.AttachedToLogicalTree += (_, _) => ++logicalAttaches;
                content.DetachedFromLogicalTree += (_, _) => ++logicalDetaches;
                content.AttachedToVisualTree += (_, _) => ++visualAttaches;
                content.DetachedFromVisualTree += (_, _) => ++visualDetaches;

                // TreeDataGridPresenterBase retains recyclable controls by hiding them while
                // leaving their logical and visual parents intact.
                cell.Unrealize();
                cell.IsVisible = false;
                Dispatcher.UIThread.RunJobs();

                Assert.Same(firstModel.Value, cell.Content);
                Assert.Same(logicalParent, content.Parent);
                Assert.Same(visualParent, content.GetVisualParent());
                Assert.Same(content, cell.GetVisualDescendants().Single(x => x is TrackingControl));

                cell.FinalizeUnrealize();

                Assert.Null(cell.Content);
                Assert.Same(logicalParent, content.Parent);
                Assert.Same(visualParent, content.GetVisualParent());
                Assert.Same(content, cell.GetVisualDescendants().Single(x => x is TrackingControl));

                cell.IsVisible = true;
                cell.Realize(factory, null, secondModel, 0, 1);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                Assert.Same(content, cell.GetVisualDescendants().Single(x => x is TrackingControl));
                Assert.Equal(1, content.ApplyTemplateCount);
                Assert.Equal(0, logicalAttaches);
                Assert.Equal(0, logicalDetaches);
                Assert.Equal(0, visualAttaches);
                Assert.Equal(0, visualDetaches);

                cell.Unrealize();
                cell.IsVisible = false;
                panel.Children.Remove(cell);

                Assert.Null(cell.Content);
            }
            finally
            {
                window.Close();
            }
        }

        private sealed class TrackingDataTemplate : IRecyclingDataTemplate
        {
            public Control? Build(object? data) => Build(data, null);

            public Control? Build(object? data, Control? existing) =>
                existing ?? new TrackingControl();

            public bool Match(object? data) => true;
        }

        private sealed class TrackingControl : TemplatedControl
        {
            public TrackingControl()
            {
                Template = new FuncControlTemplate<TrackingControl>((_, _) => new Border());
            }

            public int ApplyTemplateCount { get; private set; }

            protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
            {
                ++ApplyTemplateCount;
                base.OnApplyTemplate(e);
            }
        }
    }
}
