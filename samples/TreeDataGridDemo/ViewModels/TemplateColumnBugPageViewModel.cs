using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels
{
    internal sealed class TemplateColumnBugPageViewModel
    {
        public TemplateColumnBugPageViewModel()
        {
            Items = Enumerable.Range(1, 200)
                .Select(index => new TemplateColumnItem
                {
                    IsFlagged = index % 3 == 0,
                    Name = $"Item {index:000}",
                    Type = $"Type {(char)('A' + (index % 4))}",
                    Details = $"Details for item {index:000}"
                })
                .ToList();

            Source = CreateSource(Items);
        }

        public IReadOnlyList<TemplateColumnItem> Items { get; }

        public FlatTreeDataGridSource<TemplateColumnItem> Source { get; }

        private static FlatTreeDataGridSource<TemplateColumnItem> CreateSource(
            IEnumerable<TemplateColumnItem> items)
        {
            return new FlatTreeDataGridSource<TemplateColumnItem>(items)
            {
                Columns =
                {
                    new TemplateColumn<TemplateColumnItem>(
                        header: "Status",
                        cellTemplateResourceKey: "IsFlaggedTemplate",
                        cellEditingTemplateResourceKey: null,
                        width: new GridLength(50, GridUnitType.Pixel),
                        options: new TemplateColumnOptions<TemplateColumnItem>
                        {
                            CompareAscending = SortAscending(x => x.IsFlagged),
                            CompareDescending = SortDescending(x => x.IsFlagged),
                            CanUserResizeColumn = false,
                            CanUserSortColumn = true
                        }),
                    new TextColumn<TemplateColumnItem, string>(
                        header: "Name",
                        getter: x => x.Name,
                        width: GridLength.Star,
                        options: new TextColumnOptions<TemplateColumnItem>
                        {
                            CanUserResizeColumn = true,
                            CanUserSortColumn = true,
                            MinWidth = new GridLength(70),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            CompareAscending = SortAscending(x => x.Name),
                            CompareDescending = SortDescending(x => x.Name)
                        }),
                    new TextColumn<TemplateColumnItem, string>(
                        header: "Type",
                        getter: x => x.Type,
                        width: GridLength.Star,
                        options: new TextColumnOptions<TemplateColumnItem>
                        {
                            CanUserResizeColumn = true,
                            CanUserSortColumn = true,
                            MinWidth = new GridLength(110),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            CompareAscending = SortAscending(x => x.Type),
                            CompareDescending = SortDescending(x => x.Type)
                        }),
                    new TemplateColumn<TemplateColumnItem>(
                        header: "Details",
                        cellTemplateResourceKey: "DetailsTemplate",
                        cellEditingTemplateResourceKey: null,
                        width: GridLength.Star,
                        options: new TemplateColumnOptions<TemplateColumnItem>
                        {
                            CompareAscending = SortAscending(x => x.Details),
                            CompareDescending = SortDescending(x => x.Details),
                            CanUserResizeColumn = true,
                            CanUserSortColumn = true,
                            MinWidth = new GridLength(125)
                        })
                }
            };
        }

        private static Comparison<TemplateColumnItem?> SortAscending<TValue>(
            Func<TemplateColumnItem, TValue> selector)
        {
            return (x, y) => Compare(x, y, selector);
        }

        private static Comparison<TemplateColumnItem?> SortDescending<TValue>(
            Func<TemplateColumnItem, TValue> selector)
        {
            return (x, y) => Compare(y, x, selector);
        }

        private static int Compare<TValue>(
            TemplateColumnItem? x,
            TemplateColumnItem? y,
            Func<TemplateColumnItem, TValue> selector)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;

            return Comparer<TValue>.Default.Compare(selector(x), selector(y));
        }
    }
}
