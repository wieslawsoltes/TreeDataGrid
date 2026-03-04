// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Automation.Peers
{
    public class TreeDataGridCellAutomationPeerTests
    {
        [AvaloniaFact]
        public void Should_Create_Custom_Cell_Peer()
        {
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCellAutomationPeer>(new TreeDataGridTextCell());

            Assert.Equal(AutomationControlType.Custom, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());
            Assert.True(peer.IsContentElement());
        }

        [AvaloniaFact]
        public void Should_Use_TextCell_Value_As_Accessible_Name()
        {
            var cell = new TreeDataGridTextCell
            {
                Value = "Row 1, Column 1",
            };
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCellAutomationPeer>(cell);

            Assert.Equal("Row 1, Column 1", peer.GetName());
        }

        [AvaloniaFact]
        public void Should_Prefer_Explicit_Automation_Name()
        {
            var cell = new TreeDataGridTextCell
            {
                Value = "Fallback value",
            };
            AutomationProperties.SetName(cell, "Explicit name");
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridCellAutomationPeer>(cell);

            Assert.Equal("Explicit name", peer.GetName());
        }
    }
}
