using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = global::Uno.Controls.Models.TreeDataGrid;
using UP = global::Uno.Controls.Presentation;

namespace TreeDataGrid.Parity.Tests;

public sealed class BuiltInBindingParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Descriptor_read_and_write_are_consumed_without_changing_raw_selectors(bool writable)
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        a.Binding.Read = u.Binding.Read = static x => x.Alias;
        a.Binding.Write = u.Binding.Write = writable ? static (x, value) => x.Alias = value : null;
        Assert.Same(u.Binding, u.Binding);
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(ac.CanEdit, uc.CanEdit);
        Assert.Equal("Primary", u.ValueSelector(um));
        Assert.Equal("Primary", u.GetSearchText(um));
        if (writable)
        {
            ((A.ITextCell)ac).Text = "Changed alias";
            ((U.ITextCell)uc).Text = "Changed alias";
            Assert.Equal(am.Alias, um.Alias);
            Assert.Equal("Primary", um.Name);
        }
    }

    [Fact]
    public void Existing_and_retained_cells_keep_their_original_snapshot_while_fresh_cells_use_edits()
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name, (x, value) => x.Name = value);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name, (x, value) => x.Name = value);
        _ = u.Binding;
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        a.Binding.Read = u.Binding.Read = static x => x.Alias;
        a.Binding.Write = u.Binding.Write = static (x, value) => x.Alias = value;
        am.Name = um.Name = "Old snapshot";
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Old snapshot", uc.Value);
        var anew = new Item { Name = "Retargeted primary", Alias = "Retargeted alias" };
        var unew = new Item { Name = "Retargeted primary", Alias = "Retargeted alias" };
        Assert.True(a.TryReuseCell(ac, new Row(anew)));
        Assert.True(u.TryReuseCell(uc, new Row(unew)));
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Retargeted primary", uc.Value);
        Assert.Equal(0, am.Subscribers);
        Assert.Equal(0, um.Subscribers);
        ((A.ITextCell)ac).Text = "Still primary";
        ((U.ITextCell)uc).Text = "Still primary";
        Assert.Equal(anew.Name, unew.Name);
        Assert.Equal("Retargeted alias", unew.Alias);
        var freshA = a.CreateCell(new Row(anew));
        using var freshU = u.CreateCell(new Row(unew));
        using var freshLifetime = (IDisposable)freshA;
        Assert.Equal(freshA.Value, freshU.Value);
        Assert.Equal("Retargeted alias", freshU.Value);
    }

    [Fact]
    public void Editing_links_in_place_only_changes_subsequently_created_cells()
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        Func<Item, object> first = static x => x.First;
        Func<Item, object> second = static x => x.Second;
        a.Binding.Links = [first]; u.Binding.Links = [first];
        var am = new Item(); var um = new Item();
        var oldA = a.CreateCell(new Row(am));
        using var oldU = u.CreateCell(new Row(um));
        using var oldLifetime = (IDisposable)oldA;
        a.Binding.Links[0] = u.Binding.Links[0] = second;
        var freshA = a.CreateCell(new Row(am));
        using var freshU = u.CreateCell(new Row(um));
        using var freshLifetime = (IDisposable)freshA;
        am.Name = um.Name = "Changed without root observation";
        Assert.Equal("Primary", oldU.Value);
        Assert.Equal("Primary", freshU.Value);
        am.First.Notify(); um.First.Notify();
        Assert.Equal(oldA.Value, oldU.Value);
        Assert.Equal("Changed without root observation", oldU.Value);
        Assert.Equal("Primary", freshU.Value);
        am.Second.Notify(); um.Second.Notify();
        Assert.Equal(freshA.Value, freshU.Value);
        Assert.Equal("Changed without root observation", freshU.Value);
        Assert.Equal(1, um.First.Subscribers);
        Assert.Equal(1, um.Second.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fallback_snapshot_and_recovery_match_the_reference(bool explicitNull)
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        Func<Item, string?> read = static x => x.Failure is { } error ? throw error : x.Name;
        a.Binding.Read = u.Binding.Read = read;
        a.Binding.FallbackValue = explicitNull ? null : "First fallback";
        u.Binding.FallbackValue = explicitNull ? null : "First fallback";
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        a.Binding.FallbackValue = "Second fallback"; u.Binding.FallbackValue = "Second fallback";
        am.Failure = um.Failure = new InvalidOperationException("Expected getter failure");
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(explicitNull ? null : "First fallback", uc.Value);
        var freshA = a.CreateCell(new Row(am));
        using var freshU = u.CreateCell(new Row(um));
        using var freshLifetime = (IDisposable)freshA;
        Assert.Equal(freshA.Value, freshU.Value);
        Assert.Equal("Second fallback", freshU.Value);
        am.Failure = um.Failure = null;
        am.Name = um.Name = "Recovered";
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(freshA.Value, freshU.Value);
        Assert.Null(uc.Error);
        Assert.Null(freshU.Error);
    }

    [Fact]
    public void Typed_errors_without_fallback_keep_the_last_value()
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        a.Binding.Read = u.Binding.Read = static x => x.Failure is { } error ? throw error : x.Name;
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        am.Failure = um.Failure = new InvalidOperationException("Expected");
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Primary", uc.Value);
        Assert.Same(um.Failure, uc.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Checkbox_descriptor_controls_scalar_writeback_and_read_only_state(bool writable)
    {
        var a = new A.CheckBoxColumn<Item>("Flag", x => x.Flag);
        var u = new U.CheckBoxColumn<Item>("Flag", x => x.Flag);
        a.Binding.Read = u.Binding.Read = static x => x.AlternateFlag;
        a.Binding.Write = u.Binding.Write = writable ? static (x, value) => x.AlternateFlag = value : null;
        var am = new Item(); var um = new Item();
        var ac = (A.CheckBoxCell)a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = ac;
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(!ac.IsReadOnly, uc.CanWrite);
        Assert.Equal(a.IsThreeState, u.IsThreeState);
        if (writable)
        {
            ac.Value = null; uc.Write(null);
            Assert.Equal(am.AlternateFlag, um.AlternateFlag);
            Assert.Null(um.AlternateFlag);
            Assert.False(um.Flag);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void Cell_factory_uses_its_row_and_default_observation_not_target_mode_and_source(int mode)
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        a.Binding.Mode = (Avalonia.Data.BindingMode)mode;
        u.Binding.Mode = (global::Uno.Data.BindingMode)mode;
        a.Binding.Source = new Item { Name = "Not the cell row" };
        u.Binding.Source = new Item { Name = "Not the cell row" };
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        am.Name = um.Name = "Observed cell row";
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Observed cell row", uc.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_descriptors_fail_before_observation_and_can_be_repaired(bool missingRead)
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        var ar = a.Binding.Read; var ur = u.Binding.Read;
        var al = a.Binding.Links; var ul = u.Binding.Links;
        if (missingRead) { a.Binding.Read = null; u.Binding.Read = null; }
        else { a.Binding.Links = null; u.Binding.Links = null; }
        var am = new Item(); var um = new Item();
        Assert.Throws<InvalidOperationException>(() => a.CreateCell(new Row(am)));
        Assert.Throws<InvalidOperationException>(() => u.CreateCell(new Row(um)));
        Assert.Equal(0, am.Subscribers); Assert.Equal(0, um.Subscribers);
        a.Binding.Read = ar; u.Binding.Read = ur; a.Binding.Links = al; u.Binding.Links = ul;
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        Assert.Equal(ac.Value, uc.Value);
    }

    [Fact]
    public void Protected_builtin_binding_factory_observes_the_same_descriptor_as_cell_creation()
    {
        var a = new ExposedAvaloniaColumn(); var u = new ExposedUnoColumn();
        a.Binding.Read = u.Binding.Read = static x => x.Alias;
        a.Binding.Write = u.Binding.Write = static (x, value) => x.Alias = value;
        var am = new Item(); var um = new Item();
        var ae = a.Expression(am);
        using var ue = u.Expression(um);
        using var ac = new A.TextCell<string?>(ae, false);
        using var uc = new U.TextCell<string?>(ue, false);
        Assert.Equal(ac.Value, uc.Value);
        ac.Value = uc.Value = "Factory write";
        Assert.Equal(am.Alias, um.Alias);
        Assert.Equal("Factory write", um.Alias);
    }

    private sealed class ExposedAvaloniaColumn() : A.TextColumn<Item, string?>("Name", x => x.Name)
    {
        public Avalonia.Experimental.Data.Core.TypedBindingExpression<Item, string?> Expression(Item item) => CreateBindingExpression(item);
    }
    private sealed class ExposedUnoColumn() : U.TextColumn<Item, string?>("Name", x => x.Name)
    {
        public global::Uno.Experimental.Data.Core.TypedBindingExpression<Item, string?> Expression(Item item) => CreateBindingExpression(item);
    }
    private sealed class Row(Item model) : IRow<Item>
    {
        public Item Model => model;
        public object? Header => null;
        public TreeDataGridCore.GridLength Height { get; set; }
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class Item : INotifyPropertyChanged
    {
        private string? _name = "Primary";
        private string? _alias = "Alternate";
        private bool? _alternateFlag = true;
        private PropertyChangedEventHandler? _changed;
        public string? Name { get => _name; set { _name = value; Notify(); } }
        public string? Alias { get => _alias; set { _alias = value; Notify(); } }
        public bool? Flag => false;
        public bool? AlternateFlag { get => _alternateFlag; set { _alternateFlag = value; Notify(); } }
        public Exception? Failure { get; set; }
        public Signal First { get; } = new();
        public Signal Second { get; } = new();
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
        public void Notify() => _changed?.Invoke(this, new(null));
    }
    private sealed class Signal : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
        public void Notify() => _changed?.Invoke(this, new(null));
    }
}
