// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Automation.Provider;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Automation.Peers
{
    public class TreeDataGridAutomationPeerTests
    {
        [AvaloniaFact]
        public void Should_Create_DataGrid_Peer()
        {
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridAutomationPeer>(new TreeDataGrid());

            Assert.Equal(AutomationControlType.DataGrid, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());
            Assert.True(peer.IsContentElement());
        }

        [AvaloniaFact]
        public void Should_Expose_SelectionProvider_For_RowSelection()
        {
            var (target, _, _, _) = AutomationPeerTestHelper.CreateFlatTarget(singleSelect: false);
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridAutomationPeer>(target);
            var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(peer.GetProvider<ISelectionProvider>());

            Assert.True(selectionProvider.CanSelectMultiple);
            Assert.False(selectionProvider.IsSelectionRequired);
        }

        [AvaloniaFact]
        public void Should_Not_Expose_SelectionProvider_For_CellSelection()
        {
            var (target, _, _, _) = AutomationPeerTestHelper.CreateFlatTarget(
                singleSelect: false,
                useCellSelection: true);
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridAutomationPeer>(target);

            Assert.Null(peer.GetProvider<ISelectionProvider>());
        }

        [AvaloniaFact]
        public void Should_Return_Selected_Row_Peers_For_Realized_Selected_Rows()
        {
            var (target, source, _, root) = AutomationPeerTestHelper.CreateFlatTarget(singleSelect: true);
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridAutomationPeer>(target);
            var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(peer.GetProvider<ISelectionProvider>());

            var modelIndex = source.Rows.RowIndexToModelIndex(0);
            Assert.NotEqual(default, modelIndex);

            var selectionModel = Assert.IsAssignableFrom<ITreeSelectionModel>(target.RowSelection);
            selectionModel.Select(modelIndex);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var selectedPeers = selectionProvider.GetSelection();

            Assert.Single(selectedPeers);
            Assert.IsType<TreeDataGridRowAutomationPeer>(selectedPeers[0]);
        }
    }
}
