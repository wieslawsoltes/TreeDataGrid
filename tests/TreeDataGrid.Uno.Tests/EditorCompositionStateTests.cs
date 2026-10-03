using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class EditorCompositionStateTests
{
    [Fact]
    public void Composition_keys_remain_protected_until_its_end_dispatch_completes()
    {
        var state = new EditorCompositionState();
        Assert.False(state.BlocksEditKeys);
        state.Begin();
        Assert.True(state.BlocksEditKeys);
        var ticket = state.End();
        Assert.True(state.BlocksEditKeys);
        Assert.True(state.Complete(ticket));
        Assert.False(state.BlocksEditKeys);
        Assert.False(state.Complete(ticket));
    }

    [Fact]
    public void Old_completion_cannot_clear_a_new_composition()
    {
        var state = new EditorCompositionState();
        state.Begin();
        var old = state.End();
        state.Begin();
        Assert.False(state.Complete(old));
        Assert.True(state.BlocksEditKeys);
        var current = state.End();
        Assert.False(state.Complete(old));
        Assert.True(state.BlocksEditKeys);
        Assert.True(state.Complete(current));
        Assert.False(state.BlocksEditKeys);
    }

    [Fact]
    public void Retiring_an_editor_invalidates_all_queued_completions()
    {
        var state = new EditorCompositionState();
        state.Begin();
        var retired = state.End();
        state.Reset();
        Assert.False(state.BlocksEditKeys);
        state.Begin();
        Assert.False(state.Complete(retired));
        Assert.True(state.BlocksEditKeys);
    }

    [Fact]
    public void Duplicate_end_events_cannot_finish_the_later_dispatch_early()
    {
        var state = new EditorCompositionState();
        state.Begin();
        var first = state.End();
        var second = state.End();
        Assert.False(state.Complete(first));
        Assert.True(state.BlocksEditKeys);
        Assert.True(state.Complete(second));
        Assert.False(state.BlocksEditKeys);
    }
}
