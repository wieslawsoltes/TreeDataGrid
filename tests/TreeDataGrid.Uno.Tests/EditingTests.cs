using System;
using System.ComponentModel;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class EditingTests
{
    [Fact]
    public void Begin_and_cancel_never_write_buffered_value()
    {
        var value = new EditValue();
        var model = new Editable();
        using var edit = new CellEditSession(value, model, true);
        Assert.Equal(1, model.Begins);
        edit.Cancel();
        edit.Cancel();
        Assert.False(edit.Commit("late"));
        Assert.Equal(0, value.Writes);
        Assert.Equal(1, model.Cancels);
        Assert.Equal(0, model.Ends);
    }
    [Fact]
    public void Commit_writes_and_ends_transaction_once()
    {
        var value = new EditValue();
        var model = new Editable();
        using var edit = new CellEditSession(value, model, true);
        Assert.True(edit.Commit("new"));
        Assert.Equal("new", value.LastValue);
        Assert.Equal(1, model.Ends);
        Assert.False(edit.Commit("again"));
        edit.Cancel();
        Assert.Equal(1, value.Writes);
        Assert.Equal(0, model.Cancels);
    }
    [Fact]
    public void Failed_write_remains_editable_and_can_be_corrected()
    {
        var value = new EditValue { Fail = true };
        var model = new Editable();
        using var edit = new CellEditSession(value, model, true);
        Assert.False(edit.Commit("bad"));
        Assert.IsType<FormatException>(edit.Error);
        Assert.True(edit.IsActive);
        Assert.Equal(0, model.Ends);
        value.Fail = false;
        Assert.True(edit.Commit("correct"));
        Assert.Null(edit.Error);
        Assert.Equal(1, model.Ends);
    }
    [Fact]
    public void Template_edit_uses_model_transaction_without_writing_the_row_value()
    {
        var value = new EditValue();
        var model = new Editable();
        using var edit = new CellEditSession(value, model, false);
        Assert.True(edit.Commit(null));
        Assert.Equal(0, value.Writes);
        Assert.Equal(1, model.Ends);
    }
    [Fact]
    public void Synchronous_recycling_during_setter_cancels_old_edit()
    {
        var value = new EditValue();
        var model = new Editable();
        using var edit = new CellEditSession(value, model, true);
        value.AfterWrite = edit.Cancel;
        Assert.False(edit.Commit("old row"));
        Assert.False(edit.IsActive);
        Assert.Equal(1, model.Cancels);
        Assert.Equal(0, model.Ends);
        Assert.False(edit.Commit("new row must not receive this"));
        Assert.Equal(1, value.Writes);
    }
    [Fact]
    public void Failed_begin_cleans_up_partial_transaction()
    {
        var model = new Editable { FailBegin = true };
        Assert.Throws<InvalidOperationException>(() => new CellEditSession(new EditValue(), model, true));
        Assert.Equal(1, model.Begins);
        Assert.Equal(1, model.Cancels);
    }
    private sealed class EditValue : CellValue
    {
        public int Writes;
        public bool Fail;
        public object? LastValue;
        public Action? AfterWrite;
        public override object? Value => LastValue;
        public override bool CanEdit => true;
        public override void Write(object? value)
        {
            ++Writes;
            if (Fail) throw new FormatException("Invalid input");
            LastValue = value;
            AfterWrite?.Invoke();
        }
    }
    private sealed class Editable : IEditableObject
    {
        public int Begins, Cancels, Ends;
        public bool FailBegin;
        public void BeginEdit() { ++Begins; if (FailBegin) throw new InvalidOperationException("begin"); }
        public void CancelEdit() => ++Cancels;
        public void EndEdit() => ++Ends;
    }
}
