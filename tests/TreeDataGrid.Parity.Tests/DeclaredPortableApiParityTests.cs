using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;
using AV = Avalonia.Controls.TreeDataGridItemsSourceView;
using UV = Uno.Controls.TreeDataGridItemsSourceView;
using CoreView = TreeDataGridCore.TreeDataGridItemsSourceView;

namespace TreeDataGrid.Parity.Tests;

public sealed class DeclaredPortableApiParityTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Theory]
    [InlineData(nameof(U.ColumnOptions<Model>.CanUserResizeColumn))]
    [InlineData(nameof(U.ColumnOptions<Model>.CanUserSortColumn))]
    [InlineData(nameof(U.ColumnOptions<Model>.CompareAscending))]
    [InlineData(nameof(U.ColumnOptions<Model>.CompareDescending))]
    public void Portable_options_are_declared_on_the_native_owner_without_backing_storage(string name)
    {
        var reference = typeof(A.ColumnOptions<Model>).GetProperty(name, Declared)!;
        var native = typeof(U.ColumnOptions<Model>).GetProperty(name, Declared)!;
        Assert.NotNull(native);
        Assert.Equal(reference.PropertyType, native.PropertyType);
        Assert.Equal(reference.GetMethod!.IsPublic, native.GetMethod!.IsPublic);
        Assert.Equal(reference.SetMethod!.IsPublic, native.SetMethod!.IsPublic);
        Assert.Null(typeof(U.ColumnOptions<Model>).GetField($"<{name}>k__BackingField", Declared));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Core_and_native_option_references_share_every_portable_policy(bool? value)
    {
        var native = new U.ColumnOptions<Model>();
        TreeDataGridCore.Models.ColumnOptions<Model> core = native;
        var reference = new A.ColumnOptions<Model>();
        Comparison<Model?> compare = static (_, _) => 17;
        native.CanUserResizeColumn = reference.CanUserResizeColumn = value;
        native.CanUserSortColumn = reference.CanUserSortColumn = value;
        native.CompareAscending = reference.CompareAscending = compare;
        native.CompareDescending = reference.CompareDescending = compare;
        Assert.Equal(reference.CanUserResizeColumn, core.CanUserResizeColumn);
        Assert.Equal(reference.CanUserSortColumn, core.CanUserSortColumn);
        Assert.Same(compare, core.CompareAscending);
        Assert.Same(compare, core.CompareDescending);
        core.CanUserResizeColumn = core.CanUserSortColumn = value is null ? true : !value;
        core.CompareAscending = core.CompareDescending = null;
        Assert.Equal(core.CanUserResizeColumn, native.CanUserResizeColumn);
        Assert.Equal(core.CanUserSortColumn, native.CanUserSortColumn);
        Assert.Null(native.CompareAscending);
        Assert.Null(native.CompareDescending);
    }

    [Theory]
    [InlineData(nameof(U.ColumnBase<Model>.Header))]
    [InlineData(nameof(U.ColumnBase<Model>.Tag))]
    [InlineData(nameof(U.ColumnBase<Model>.SortDirection))]
    public void Column_state_is_declared_on_the_native_owner(string name)
    {
        var reference = typeof(A.ColumnBase<Model>).GetProperty(name, Declared)!;
        var native = typeof(U.ColumnBase<Model>).GetProperty(name, Declared)!;
        Assert.NotNull(native);
        Assert.Equal(reference.PropertyType, native.PropertyType);
        Assert.True(native.GetMethod!.IsPublic);
        Assert.True(native.SetMethod!.IsPublic);
        Assert.Null(typeof(U.ColumnBase<Model>).GetField($"<{name}>k__BackingField", Declared));
    }

    [Fact]
    public void Column_forwarding_preserves_notification_order_and_layout_base_identity()
    {
        var a = new ReferenceColumn();
        var u = new NativeColumn();
        Uno.Controls.Presentation.CellColumnBase<Model> layout = u;
        var expected = new List<string?>();
        var actual = new List<string?>();
        a.PropertyChanged += (_, e) => expected.Add(e.PropertyName);
        u.PropertyChanged += (sender, e) => { Assert.Same(u, sender); actual.Add(e.PropertyName); };
        var tag = new object();
        a.Header = u.Header = "first";
        a.Header = layout.Header = "second";
        a.Header = u.Header = "second";
        a.SortDirection = u.SortDirection = ListSortDirection.Ascending;
        a.SortDirection = layout.SortDirection = ListSortDirection.Descending;
        a.Tag = u.Tag = tag;
        Assert.Equal(expected, actual);
        Assert.Equal(a.Header, u.Header);
        Assert.Equal(a.Header, layout.Header);
        Assert.Equal(a.SortDirection, u.SortDirection);
        Assert.Same(tag, layout.Tag);
        Assert.Same(tag, u.Tag);
    }

    [Fact]
    public void Checkbox_state_is_declared_and_preserves_nullable_and_nonnullable_policy()
    {
        var reference = typeof(A.CheckBoxColumn<Model>).GetProperty("IsThreeState", Declared)!;
        var native = typeof(U.CheckBoxColumn<Model>).GetProperty("IsThreeState", Declared)!;
        Assert.NotNull(native);
        Assert.Equal(reference.PropertyType, native.PropertyType);
        Assert.Null(native.SetMethod);
        var a = new A.CheckBoxColumn<Model>("Flag", x => x.Flag);
        using var u = new U.CheckBoxColumn<Model>("Flag", x => x.Flag);
        var an = new A.CheckBoxColumn<Model>("Nullable", x => x.NullableFlag);
        using var un = new U.CheckBoxColumn<Model>("Nullable", x => x.NullableFlag);
        Assert.Equal(a.IsThreeState, u.IsThreeState);
        Assert.Equal(an.IsThreeState, un.IsThreeState);
        Assert.False(u.IsThreeState);
        Assert.True(un.IsThreeState);
    }

    [Theory]
    [InlineData("Count")]
    [InlineData("Inner")]
    [InlineData("Item")]
    [InlineData("HasKeyIndexMapping")]
    [InlineData("CollectionChanged")]
    [InlineData("GetAt")]
    [InlineData("IndexOf")]
    [InlineData("KeyFromIndex")]
    [InlineData("IndexFromKey")]
    [InlineData("Dispose")]
    [InlineData("OnItemsSourceChanged")]
    public void Items_view_declares_the_same_portable_member_kind(string name)
    {
        var expected = Assert.Single(typeof(AV).GetMember(name, Declared));
        var actual = Assert.Single(typeof(UV).GetMember(name, Declared));
        Assert.Equal(expected.MemberType, actual.MemberType);
        Assert.Equal(typeof(UV), actual.DeclaringType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Adding_and_removing_through_different_static_types_uses_one_event_store(bool nativeFirst)
    {
        var items = new ObservableCollection<string>();
        using var native = new UV(items);
        CoreView core = native;
        INotifyCollectionChanged contract = native;
        var calls = 0;
        NotifyCollectionChangedEventHandler handler = (sender, _) =>
        {
            Assert.Same(native, sender);
            ++calls;
        };
        if (nativeFirst) native.CollectionChanged += handler;
        else contract.CollectionChanged += handler;
        items.Add("observed");
        Assert.Equal(1, calls);
        if (nativeFirst) core.CollectionChanged -= handler;
        else native.CollectionChanged -= handler;
        items.Add("unobserved");
        Assert.Equal(1, calls);
        native.CollectionChanged += handler;
        core.CollectionChanged += handler;
        contract.CollectionChanged -= handler;
        items.Add("one remaining subscription");
        Assert.Equal(2, calls);
        native.CollectionChanged -= handler;
    }

    [Fact]
    public void Subclass_hook_forwards_the_original_event_and_observer_failure()
    {
        var a = new ReferenceView(Array.Empty<object>());
        var u = new NativeView(Array.Empty<object>());
        using (a)
        using (u)
        {
            var change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            var expected = new InvalidOperationException("observer");
            NotifyCollectionChangedEventArgs? referenceEvent = null;
            NotifyCollectionChangedEventArgs? nativeEvent = null;
            a.CollectionChanged += (_, e) => { referenceEvent = e; throw expected; };
            ((CoreView)u).CollectionChanged += (_, e) => { nativeEvent = e; throw expected; };
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => a.Notify(change)));
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => u.Notify(change)));
            Assert.Same(change, referenceEvent);
            Assert.Same(change, nativeEvent);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Disposal_through_any_contract_retires_the_same_borrowed_view(int route)
    {
        var items = new ObservableCollection<string> { "first" };
        var native = new UV(items);
        CoreView core = native;
        var calls = 0;
        native.CollectionChanged += (_, _) => ++calls;
        switch (route)
        {
            case 0: native.Dispose(); break;
            case 1: core.Dispose(); break;
            default: ((IDisposable)native).Dispose(); break;
        }
        native.Dispose();
        items.Add("still owned by caller");
        Assert.Equal(0, calls);
        Assert.Equal(2, items.Count);
        Assert.Throws<ObjectDisposedException>(() => native.Count);
        Assert.Throws<ObjectDisposedException>(() => core.Count);
    }

    private sealed class Model
    {
        public bool Flag { get; set; }
        public bool? NullableFlag { get; set; }
    }

    private sealed class ReferenceColumn() : A.ColumnBase<Model>("Initial", null, new())
    {
        public override Comparison<Model?>? GetComparison(ListSortDirection direction) => null;
        public override A.ICell CreateCell(A.IRow<Model> row) => new A.TextCell<int>(0);
    }

    private sealed class NativeColumn() : U.ColumnBase<Model>("Initial", null, new())
    {
        public override Comparison<Model?>? GetComparison(ListSortDirection direction) => null;
        public override U.ICell CreateCell(TreeDataGridCore.Models.IRow<Model> row) => new U.TextCell<int>(0);
    }

    private sealed class ReferenceView(IEnumerable source) : AV(source)
    {
        public void Notify(NotifyCollectionChangedEventArgs args) => OnItemsSourceChanged(args);
    }

    private sealed class NativeView(IEnumerable source) : UV(source)
    {
        public void Notify(NotifyCollectionChangedEventArgs args) => OnItemsSourceChanged(args);
    }
}
