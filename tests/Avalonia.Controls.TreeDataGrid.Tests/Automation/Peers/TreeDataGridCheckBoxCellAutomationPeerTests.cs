// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Automation.Peers
{
    public class TreeDataGridCheckBoxCellAutomationPeerTests
    {
        [AvaloniaFact]
        public void Should_Create_CheckBox_Cell_Peer()
        {
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCheckBoxCellAutomationPeer>(
                new TreeDataGridCheckBoxCell());

            Assert.Equal(AutomationControlType.CheckBox, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());
            Assert.True(peer.IsContentElement());
        }

        [AvaloniaFact]
        public void Should_Map_Value_To_ToggleState()
        {
            var cell = new TreeDataGridCheckBoxCell { Value = false };
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCheckBoxCellAutomationPeer>(cell);
            var provider = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());

            Assert.Equal(ToggleState.Off, provider.ToggleState);
            cell.Value = true;
            Assert.Equal(ToggleState.On, provider.ToggleState);
            cell.Value = null;
            Assert.Equal(ToggleState.Indeterminate, provider.ToggleState);
        }

        [AvaloniaFact]
        public void Toggle_Should_Cycle_TwoState_Values()
        {
            var cell = new TreeDataGridCheckBoxCell
            {
                IsThreeState = false,
                Value = false,
            };
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCheckBoxCellAutomationPeer>(cell);
            var provider = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());

            provider.Toggle();
            Assert.True(cell.Value);
            provider.Toggle();
            Assert.False(cell.Value);
        }

        [AvaloniaFact]
        public void Toggle_Should_Cycle_ThreeState_Values()
        {
            var cell = new TreeDataGridCheckBoxCell
            {
                IsThreeState = true,
                Value = true,
            };
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCheckBoxCellAutomationPeer>(cell);
            var provider = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());

            provider.Toggle();
            Assert.Null(cell.Value);
            provider.Toggle();
            Assert.False(cell.Value);
            provider.Toggle();
            Assert.True(cell.Value);
        }

        [AvaloniaFact]
        public void Toggle_Should_Throw_For_ReadOnly_Cell()
        {
            var cell = new TreeDataGridCheckBoxCell
            {
                IsReadOnly = true,
                Value = false,
            };
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCheckBoxCellAutomationPeer>(cell);
            var provider = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());

            Assert.Throws<NotSupportedException>(() => provider.Toggle());
        }
    }
}
