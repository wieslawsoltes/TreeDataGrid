// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Automation.Peers
{
    public class TreeDataGridRowAutomationPeerTests
    {
        [AvaloniaFact]
        public void Should_Create_TreeItem_Peer()
        {
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(new TreeDataGridRow());

            Assert.Equal(AutomationControlType.TreeItem, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());
            Assert.True(peer.IsContentElement());
        }

        [AvaloniaFact]
        public void Should_Expose_Toggle_Only_For_Visible_Expanders()
        {
            var nonExpanderRow = CreateRealizedRow(new TestRows(new TestRow(new object())));
            var noExpanderPeer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(
                nonExpanderRow);

            var hiddenExpanderRow = new TestExpanderRow(
                model: new object(),
                showExpander: false,
                isExpanded: false);
            var hiddenExpanderOwner = CreateRealizedRow(new TestRows(hiddenExpanderRow));
            var hiddenExpanderPeer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(
                hiddenExpanderOwner);

            var expanderRow = new TestExpanderRow(
                model: new object(),
                showExpander: true,
                isExpanded: false);
            var expandableRow = CreateRealizedRow(new TestRows(expanderRow));
            var expandablePeer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(
                expandableRow);
            var toggleProvider = Assert.IsAssignableFrom<IToggleProvider>(expandablePeer.GetProvider<IToggleProvider>());

            Assert.Null(noExpanderPeer.GetProvider<IToggleProvider>());
            Assert.Null(hiddenExpanderPeer.GetProvider<IToggleProvider>());
            Assert.Equal(ToggleState.Off, toggleProvider.ToggleState);

            toggleProvider.Toggle();

            Assert.True(expanderRow.IsExpanded);
            Assert.Equal(ToggleState.On, toggleProvider.ToggleState);
        }

        [AvaloniaFact]
        public void Should_Expose_ExpandCollapse_Only_For_Visible_Expanders()
        {
            var nonExpanderRow = CreateRealizedRow(new TestRows(new TestRow(new object())));
            var nonExpanderPeer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(
                nonExpanderRow);

            var expanderRow = new TestExpanderRow(
                model: new object(),
                showExpander: true,
                isExpanded: false);
            var expandableRow = CreateRealizedRow(new TestRows(expanderRow));
            var expandablePeer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(
                expandableRow);
            var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                expandablePeer.GetProvider<IExpandCollapseProvider>());

            Assert.Null(nonExpanderPeer.GetProvider<IExpandCollapseProvider>());
            Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);

            provider.Expand();
            Assert.True(expanderRow.IsExpanded);
            Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

            provider.Collapse();
            Assert.False(expanderRow.IsExpanded);
            Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        }

        [AvaloniaFact]
        public void Toggle_Should_Throw_When_Row_Is_Disabled()
        {
            var row = CreateRealizedRow(new TestRows(new TestExpanderRow(
                model: new object(),
                showExpander: true,
                isExpanded: false)));
            row.IsEnabled = false;
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(row);
            var toggleProvider = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());

            Assert.Throws<ElementNotEnabledException>(() => toggleProvider.Toggle());
        }

        [AvaloniaFact]
        public void ValueProvider_Should_Be_ReadOnly()
        {
            var row = CreateRealizedRow(new TestRows(new TestRow(new TestRowModel("Node A"))));
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(row);
            var valueProvider = Assert.IsAssignableFrom<IValueProvider>(peer.GetProvider<IValueProvider>());

            Assert.True(valueProvider.IsReadOnly);
            Assert.Equal("Node A", valueProvider.Value);
            Assert.Throws<NotSupportedException>(() => valueProvider.SetValue("Updated"));
        }

        [AvaloniaFact]
        public void SelectionItemProvider_Should_Select_And_Deselect_Row()
        {
            var (target, source, _, root) = AutomationPeerTestHelper.CreateFlatTarget(singleSelect: false);
            var row = Assert.IsType<TreeDataGridRow>(target.TryGetRow(0));
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridRowAutomationPeer>(row);
            var selectionProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
                peer.GetProvider<ISelectionItemProvider>());
            var modelIndex = source.Rows.RowIndexToModelIndex(row.RowIndex);
            var selectionModel = Assert.IsAssignableFrom<ITreeSelectionModel>(target.RowSelection);

            Assert.NotNull(selectionProvider.SelectionContainer);
            Assert.False(selectionProvider.IsSelected);

            selectionProvider.Select();
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(selectionModel.IsSelected(modelIndex));
            Assert.True(selectionProvider.IsSelected);

            selectionProvider.RemoveFromSelection();
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.False(selectionModel.IsSelected(modelIndex));
            Assert.False(selectionProvider.IsSelected);

            selectionProvider.AddToSelection();
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(selectionModel.IsSelected(modelIndex));
            Assert.True(selectionProvider.IsSelected);
        }

        private static TreeDataGridRow CreateRealizedRow(IRows rows)
        {
            var row = new TreeDataGridRow();
            row.Realize(
                elementFactory: null,
                selection: null,
                columns: null,
                rows: rows,
                rowIndex: 0);
            return row;
        }

        private sealed class TestRows : IRows
        {
            private readonly IReadOnlyList<IRow> _rows;

            public TestRows(params IRow[] rows)
            {
                _rows = rows;
            }

            public int Count => _rows.Count;

            public IRow this[int index] => _rows[index];

            public (int index, double y) GetRowAt(double y) => (0, 0);

            public int ModelIndexToRowIndex(IndexPath modelIndex) => -1;

            public IndexPath RowIndexToModelIndex(int rowIndex) => default;

            public ICell RealizeCell(IColumn column, int columnIndex, int rowIndex)
            {
                throw new NotSupportedException();
            }

            public void UnrealizeCell(ICell cell, int columnIndex, int rowIndex)
            {
            }

            public IEnumerator<IRow> GetEnumerator() => _rows.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public event NotifyCollectionChangedEventHandler? CollectionChanged
            {
                add { }
                remove { }
            }
        }

        private class TestRow : IRow
        {
            public TestRow(object? model)
            {
                Model = model;
            }

            public object? Header => null;

            public GridLength Height { get; set; } = GridLength.Auto;

            public object? Model { get; }
        }

        private sealed class TestExpanderRow : TestRow, IExpander
        {
            public TestExpanderRow(object? model, bool showExpander, bool isExpanded)
                : base(model)
            {
                ShowExpander = showExpander;
                IsExpanded = isExpanded;
            }

            public bool IsExpanded { get; set; }
            public bool ShowExpander { get; set; }
        }

        private sealed class TestRowModel
        {
            private readonly string _name;

            public TestRowModel(string name)
            {
                _name = name;
            }

            public override string ToString()
            {
                return _name;
            }
        }
    }
}
