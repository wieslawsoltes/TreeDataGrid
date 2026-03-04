using System.Linq;
using Avalonia.Collections;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Automation.Peers
{
    internal static class AutomationPeerTestHelper
    {
        public static TPeer CreatePeer<TPeer>(Control control)
            where TPeer : AutomationPeer
        {
            return Assert.IsType<TPeer>(ControlAutomationPeer.CreatePeerForElement(control));
        }

        public static (
            TreeDataGrid target,
            FlatTreeDataGridSource<FlatRowModel> source,
            AvaloniaList<FlatRowModel> items,
            TestWindow root) CreateFlatTarget(
            bool singleSelect = true,
            int itemCount = 20,
            bool useCellSelection = false,
            bool runLayout = true)
        {
            var items = new AvaloniaList<FlatRowModel>(
                Enumerable.Range(0, itemCount).Select(x => new FlatRowModel
                {
                    Id = x,
                    Title = $"Item {x}",
                }));

            var source = new FlatTreeDataGridSource<FlatRowModel>(items);
            source.RowSelection!.SingleSelect = singleSelect;
            source.Columns.Add(new TextColumn<FlatRowModel, int>("ID", x => x.Id));
            source.Columns.Add(new TextColumn<FlatRowModel, string?>("Title", x => x.Title, (o, v) => o.Title = v));

            if (useCellSelection)
            {
                source.Selection = new TreeDataGridCellSelectionModel<FlatRowModel>(source);
            }

            var target = new TreeDataGrid
            {
                Template = TestTemplates.TreeDataGridTemplate(),
                Source = source,
            };

            var root = new TestWindow(target)
            {
                Styles =
                {
                    new Style(x => x.Is<TreeDataGridRow>())
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridRow.TemplateProperty, TestTemplates.TreeDataGridRowTemplate()),
                        }
                    },
                    new Style(x => x.Is<TreeDataGridCell>())
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridCell.HeightProperty, 10.0),
                        }
                    }
                }
            };

            if (runLayout)
            {
                root.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }

            return (target, source, items, root);
        }

        public sealed class FlatRowModel
        {
            public int Id { get; set; }
            public string? Title { get; set; }
        }
    }
}
