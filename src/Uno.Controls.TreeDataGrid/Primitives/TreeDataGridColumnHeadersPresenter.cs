using System.Collections.Generic;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinSize = Windows.Foundation.Size;
using WinRect = Windows.Foundation.Rect;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridColumnHeadersPresenter : Panel
    {
        private readonly List<TreeDataGridColumnHeader> _headers = new();
        private IReadOnlyList<double>? _columnWidths;

        internal TreeDataGrid? Owner { get; set; }
        internal TreeDataGridElementFactory? ElementFactory { get; set; }
        internal IColumns? Items { get; set; }
        internal IReadOnlyList<TreeDataGridColumnHeader> RealizedHeaders => _headers;

        public void Realize()
        {
            Unrealize();

            if (Owner is null || ElementFactory is null || Items is null)
                return;

            for (var columnIndex = 0; columnIndex < Items.Count; ++columnIndex)
            {
                var header = (TreeDataGridColumnHeader)ElementFactory.GetOrCreateElement(Items[columnIndex]);
                header.Click += OnHeaderClick;
                header.Realize(Owner, Items, columnIndex);
                _headers.Add(header);
                Children.Add(header);
            }

            InvalidateMeasure();
        }

        public void Unrealize()
        {
            foreach (var header in _headers)
            {
                header.Click -= OnHeaderClick;
                header.Unrealize();
                ElementFactory?.RecycleElement(header);
            }

            _headers.Clear();
            Children.Clear();
        }

        public void ApplyColumnWidths(IReadOnlyList<double> widths)
        {
            _columnWidths = widths;
            Width = widths.Count == 0 ? double.NaN : System.Linq.Enumerable.Sum(widths);
            InvalidateMeasure();
        }

        public TreeDataGridColumnHeader? TryGetElement(int columnIndex)
        {
            return columnIndex >= 0 && columnIndex < _headers.Count ? _headers[columnIndex] : null;
        }

        public TreeDataGridColumnHeader? BringIntoView(int columnIndex)
        {
            var header = TryGetElement(columnIndex);
            header?.StartBringIntoView();
            return header;
        }

        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridColumnHeadersPresenterAutomationPeer(this);
        }

        protected override WinSize MeasureOverride(WinSize availableSize)
        {
            double width = 0;
            double height = 0;

            for (var i = 0; i < _headers.Count; ++i)
            {
                var child = _headers[i];
                var childWidth = _columnWidths is not null && i < _columnWidths.Count
                    ? _columnWidths[i]
                    : double.PositiveInfinity;
                child.Measure(new WinSize(childWidth, availableSize.Height));
                width += _columnWidths is not null && i < _columnWidths.Count ? _columnWidths[i] : child.DesiredSize.Width;
                height = System.Math.Max(height, child.DesiredSize.Height);
            }

            return new WinSize(width, height);
        }

        protected override WinSize ArrangeOverride(WinSize finalSize)
        {
            double x = 0;

            for (var i = 0; i < _headers.Count; ++i)
            {
                var child = _headers[i];
                var width = _columnWidths is not null && i < _columnWidths.Count ? _columnWidths[i] : child.DesiredSize.Width;
                child.Arrange(new WinRect(x, 0, width, finalSize.Height));
                x += width;
            }

            return finalSize;
        }

        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (sender is TreeDataGridColumnHeader header)
                Owner?.HandleHeaderClick(header);
        }
    }
}
