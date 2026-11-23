using System;
using System.ComponentModel;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;
using AB = Avalonia.Experimental.Data;
using UB = Uno.Experimental.Data;
using AV = Avalonia.Data;
using UV = Uno.Data;

namespace TreeDataGrid.Parity.Tests;

public sealed class TypedCellConstructorParityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Zażółć 🐈")]
    public void Typed_text_cells_share_reference_read_and_write_behavior(string? value)
    {
        var model = new Model { Text = value };
        var ab = AB.TypedBinding<Model>.TwoWay(x => x.Text, (x, text) => x.Text = text).Instance(model);
        using var ub = UB.TypedBinding<Model>.TwoWay(x => x.Text, (x, text) => x.Text = text).Instance(model);
        using var a = new A.TextCell<string?>(ab, false);
        using var u = new U.TextCell<string?>(ub, false);
        Assert.Equal(a.Value, u.Value);
        Assert.Equal(a.Text, u.Text);
        Assert.Equal(a.CanEdit, u.CanEdit);
        Assert.Equal(a.IsReadOnly, u.IsReadOnly);
        model.Text = "External";
        Assert.Equal(a.Value, u.Value);
        u.Text = "Native";
        Assert.Equal("Native", model.Text);
        Assert.Equal(a.Value, u.Value);
        a.Text = "Original";
        Assert.Equal("Original", model.Text);
        Assert.Equal(a.Value, u.Value);
        a.Dispose(); u.Dispose();
        Assert.Equal(0, model.Subscribers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Typed_checkbox_constructors_match_three_state_reference_values(bool? initial)
    {
        var model = new Model { Flag = initial };
        var ab = AB.TypedBinding<Model>.TwoWay(x => x.Flag, (x, value) => x.Flag = value).Instance(model);
        using var ub = UB.TypedBinding<Model>.TwoWay(x => x.Flag, (x, value) => x.Flag = value).Instance(model);
        using var a = new A.CheckBoxCell(ab, false, true);
        using var u = new U.CheckBoxCell(ub, false, true);
        Assert.Equal(a.Value, u.Value);
        Assert.Equal(a.IsThreeState, u.IsThreeState);
        Assert.Equal(a.CanEdit, u.CanEdit);
        Assert.Equal(a.SingleTapEdit, u.SingleTapEdit);
        foreach (var value in new bool?[] { true, null, false })
        {
            u.Value = value;
            Assert.Equal(value, model.Flag);
            Assert.Equal(a.Value, u.Value);
        }
        a.Value = true;
        Assert.True(model.Flag);
        Assert.True(u.Value);
        a.Dispose(); u.Dispose();
        Assert.Equal(0, model.Subscribers);
    }

    [Fact]
    public void Typed_cell_readonly_policy_matches_the_reference()
    {
        var model = new Model { Text = "Source" };
        var ab = AB.TypedBinding<Model>.OneWay(x => x.Text).Instance(model);
        using var ub = UB.TypedBinding<Model>.OneWay(x => x.Text).Instance(model);
        using var a = new A.TextCell<string?>(ab, true);
        using var u = new U.TextCell<string?>(ub, true);
        a.Value = "Local";
        u.Value = "Local";
        Assert.Equal(a.Value, u.Value);
        Assert.Equal("Source", model.Text);
        model.Text = "Changed source";
        Assert.Equal(a.Value, u.Value);
        Assert.Equal("Changed source", u.Text);
    }

    private sealed class Model : INotifyPropertyChanged
    {
        private string? _text;
        private bool? _flag;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public string? Text { get => _text; set { _text = value; _changed?.Invoke(this, new(nameof(Text))); } }
        public bool? Flag { get => _flag; set { _flag = value; _changed?.Invoke(this, new(nameof(Flag))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
