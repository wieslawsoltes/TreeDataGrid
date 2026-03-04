// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Automation.Peers
{
    public class TreeDataGridColumnHeadersPresenterAutomationPeerTests
    {
        [AvaloniaFact]
        public void Should_Create_Header_Peer()
        {
            var peer = AutomationPeerTestHelper.CreatePeer<TreeDataGridColumnHeadersPresenterAutomationPeer>(
                new TreeDataGridColumnHeadersPresenter());

            Assert.Equal(AutomationControlType.Header, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());
            Assert.False(peer.IsContentElement());
        }
    }
}
