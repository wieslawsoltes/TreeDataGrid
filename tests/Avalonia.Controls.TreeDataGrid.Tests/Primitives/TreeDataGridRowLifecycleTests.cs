using System.Collections.Generic;
using Avalonia.Controls.Primitives;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TreeDataGridRowLifecycleTests
    {
        [Fact]
        public void Derived_Row_Receives_Synchronous_Lifecycle_Hooks()
        {
            var target = new RecordingRow();

            target.Realize(null, null, null, null, 4);
            target.UpdateIndex(7);
            target.Unrealize();

            Assert.Equal(
                new[]
                {
                    "realized:4:4",
                    "index:4:7:7",
                    "unrealizing:7:Recycle:7",
                },
                target.Events);
            Assert.Equal(-1, target.RowIndex);
        }

        [Fact]
        public void Item_Removal_Is_Distinguished_From_Recycling()
        {
            var target = new RecordingRow();

            target.Realize(null, null, null, null, 2);
            target.UnrealizeOnItemRemoved();

            Assert.Equal(
                new[]
                {
                    "realized:2:2",
                    "unrealizing:2:ItemRemoved:2",
                },
                target.Events);
            Assert.Equal(-1, target.RowIndex);
        }

        private sealed class RecordingRow : TreeDataGridRow
        {
            public List<string> Events { get; } = new();

            protected override void OnRealized(int rowIndex)
            {
                Events.Add($"realized:{rowIndex}:{RowIndex}");
            }

            protected override void OnRowIndexChanged(int oldRowIndex, int newRowIndex)
            {
                Events.Add($"index:{oldRowIndex}:{newRowIndex}:{RowIndex}");
            }

            protected override void OnUnrealizing(
                int rowIndex,
                TreeDataGridRowUnrealizeReason reason)
            {
                Events.Add($"unrealizing:{rowIndex}:{reason}:{RowIndex}");
            }
        }
    }
}
