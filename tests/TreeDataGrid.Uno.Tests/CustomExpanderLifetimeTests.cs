using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class CustomExpanderLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Newer_content_getter_result_wins_and_obsolete_content_is_not_subscribed(bool reset)
    {
        var old = new TextProbe("old"); var obsolete = new TextProbe("obsolete"); var current = new TextProbe("current");
        var model = new Expander(old);
        using var value = Adapt(model);
        model.OnContentRead = () => model.Replace(current);
        model.Replace(obsolete, reset);
        Assert.Same(current, value.Content);
        Assert.Equal("current", value.DisplayText);
        Assert.Equal(0, old.Subscribers); Assert.Equal(0, obsolete.Subscribers); Assert.Equal(1, current.Subscribers);
        value.Write("committed"); Assert.Equal("committed", current.Text); Assert.Equal(0, obsolete.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Returning_child_attachment_is_released_after_parent_retirement(bool beforeAttachment)
    {
        var old = new TextProbe("old"); var candidate = new TextProbe("candidate"); var model = new Expander(old);
        using var value = Adapt(model);
        if (beforeAttachment) candidate.BeforeAdd = _ => value.Dispose();
        else candidate.AfterAdd = _ => value.Dispose();
        model.Replace(candidate);
        Assert.Equal(0, candidate.Subscribers); Assert.Equal(0, old.Subscribers); Assert.Equal(0, model.Subscribers);
        Assert.Equal(1, model.Disposals); Assert.Equal(0, candidate.Disposals); Assert.Equal(0, old.Disposals);
        Assert.False(value.CanWrite); Assert.Throws<ObjectDisposedException>(() => value.Write("retired"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Newer_child_attachment_change_wins_without_retaining_the_candidate(bool beforeAttachment)
    {
        var old = new TextProbe("old"); var candidate = new TextProbe("candidate"); var current = new TextProbe("current");
        var model = new Expander(old); using var value = Adapt(model);
        if (beforeAttachment) candidate.BeforeAdd = _ => model.Replace(current);
        else candidate.AfterAdd = _ => model.Replace(current);
        model.Replace(candidate);
        Assert.Same(current, value.Content);
        Assert.Equal(0, old.Subscribers); Assert.Equal(0, candidate.Subscribers); Assert.Equal(1, current.Subscribers);
    }

    [Fact]
    public void Newer_child_metadata_change_wins_before_a_candidate_is_installed()
    {
        var old = new TextProbe("old"); var candidate = new TextProbe("candidate"); var current = new TextProbe("current");
        var model = new Expander(old); using var value = Adapt(model);
        candidate.OnAlignmentRead = () => model.Replace(current);
        model.Replace(candidate);
        Assert.Same(current, value.Content); Assert.Equal(0, candidate.Subscribers); Assert.Equal(1, current.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Old_content_detachment_cannot_publish_a_superseded_value(bool retire)
    {
        var old = new TextProbe("old"); var candidate = new TextProbe("candidate"); var current = new TextProbe("current");
        var model = new Expander(old); using var value = Adapt(model);
        var published = new List<object?>();
        value.PropertyChanged += (_, e) => { if (e is CellContentChangedEventArgs) published.Add(value.Content); };
        old.OnRemove = () => { if (retire) value.Dispose(); else model.Replace(current); };
        model.Replace(candidate);
        Assert.Equal(0, old.Subscribers); Assert.Equal(0, candidate.Subscribers);
        if (retire) { Assert.Empty(published); Assert.Equal(0, model.Subscribers); Assert.Equal(1, model.Disposals); }
        else { Assert.Same(current, Assert.Single(published)); Assert.Equal(1, current.Subscribers); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Content_notification_reentry_preserves_newer_value_and_interrupted_reset(bool reset)
    {
        var model = new Expander(new TextProbe("old")); using var value = Adapt(model);
        var current = new TextProbe("current"); var changes = new List<string?>(); var published = new List<object?>();
        var replaced = false;
        value.PropertyChanged += (_, e) =>
        {
            changes.Add(e.PropertyName);
            if (e.PropertyName == "Content" && !replaced) { replaced = true; model.Replace(current); }
            if (e is CellContentChangedEventArgs) published.Add(value.Content);
        };
        model.Replace(new TextProbe("obsolete"), reset);
        Assert.Same(current, value.Content); Assert.Same(current, Assert.Single(published));
        if (reset) { Assert.Contains("IsExpanded", changes); Assert.Contains("ShowExpander", changes); Assert.Contains("Row", changes); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parent_add_accessor_retirement_waits_for_attachment_to_finish(bool ownsModel)
    {
        var text = new TextProbe("text"); var model = new Expander(text);
        model.BeforeAdd = handler => ((IDisposable)handler.Target!).Dispose();
        model.OnContentRead = () => throw new InvalidOperationException("Retired content getter must not run.");
        using var value = Adapt(model, ownsModel);
        Assert.Equal(0, model.Subscribers); Assert.Equal(0, text.Subscribers);
        Assert.Equal(ownsModel ? 1 : 0, model.Disposals); Assert.False(value.CanEdit); Assert.False(value.CanWrite);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parent_add_failure_rolls_back_and_preserves_primary_error(bool ownsModel)
    {
        var text = new TextProbe("text"); var failure = new InvalidOperationException("parent add");
        var model = new Expander(text) { AddFailure = failure };
        Assert.Same(failure, Record.Exception(() => Adapt(model, ownsModel)));
        Assert.Equal(0, model.Subscribers); Assert.Equal(0, text.Subscribers);
        Assert.Equal(ownsModel ? 1 : 0, model.Disposals); Assert.Equal(0, text.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initial_child_metadata_failure_releases_the_parent_subscription(bool ownsModel)
    {
        var primary = new InvalidOperationException("child metadata");
        var text = new TextProbe("text") { AlignmentFailure = primary }; var model = new Expander(text);
        Assert.Same(primary, Record.Exception(() => Adapt(model, ownsModel)));
        Assert.Equal(0, model.Subscribers); Assert.Equal(0, text.Subscribers);
        Assert.Equal(ownsModel ? 1 : 0, model.Disposals); Assert.Equal(0, text.Disposals);
    }

    [Fact]
    public void Failed_replacement_disables_old_writes_and_can_recover_without_leaks()
    {
        var old = new TextProbe("old"); var failure = new InvalidOperationException("candidate add");
        var candidate = new TextProbe("candidate") { AddFailure = failure }; var model = new Expander(old);
        using var value = Adapt(model);
        Assert.Same(failure, Record.Exception(() => model.Replace(candidate)));
        Assert.Same(old, value.Content); Assert.Equal(1, old.Subscribers); Assert.Equal(0, candidate.Subscribers);
        Assert.False(value.CanWrite); Assert.False(value.CanEdit);
        Assert.Throws<InvalidOperationException>(() => value.Write("must not reach old row"));
        candidate.AddFailure = null; model.NotifyContent();
        Assert.Same(candidate, value.Content); Assert.Equal(0, old.Subscribers); Assert.Equal(1, candidate.Subscribers);
        value.Write("recovered"); Assert.Equal("recovered", candidate.Text); Assert.Equal(0, old.Writes);
    }

    [Fact]
    public void Committed_replacement_with_cleanup_failure_can_retry_its_value_notification()
    {
        var failure = new InvalidOperationException("old remove"); var old = new TextProbe("old") { RemoveFailure = failure };
        var current = new TextProbe("current"); var model = new Expander(old); using var value = Adapt(model);
        var published = 0; value.PropertyChanged += (_, e) => { if (e is CellContentChangedEventArgs) ++published; };
        Assert.Same(failure, Record.Exception(() => model.Replace(current)));
        Assert.Same(current, value.Content); Assert.Equal(0, old.Subscribers); Assert.Equal(1, current.Subscribers);
        Assert.Equal(0, published); model.NotifyContent(); Assert.Equal(1, published);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Permission_visibility_and_gesture_getters_cannot_return_live_state_after_retirement(int property)
    {
        var native = new NativeValue(); var model = new Expander(native); using var value = Adapt(model);
        if (property == 0) model.OnPermissionRead = value.Dispose;
        if (property == 1) native.OnPermissionRead = value.Dispose;
        if (property == 2) model.OnVisibilityRead = value.Dispose;
        if (property == 3) model.OnGesturesRead = value.Dispose;
        Assert.False(property switch { 0 => value.CanEdit, 1 => value.CanWrite, 2 => value.ShowExpander,
            _ => value.EditGestures != UI.BeginEditGestures.None });
        Assert.Equal(0, native.Disposals); Assert.Equal(1, model.Disposals);
    }

    [Fact]
    public void Permission_getter_cannot_return_an_old_result_after_content_replacement()
    {
        var old = new TextProbe("old"); var model = new Expander(old); using var value = Adapt(model);
        var current = new TextProbe("current"); model.OnPermissionRead = () => model.Replace(current);
        Assert.False(value.CanEdit); Assert.Same(current, value.Content); Assert.True(value.CanEdit);
    }

    [Fact]
    public void Disposal_blocks_borrowed_native_writes_and_expansion_without_disposing_native_content()
    {
        var native = new NativeValue(); var model = new Expander(native); using var value = Adapt(model);
        value.Dispose();
        model.OnPermissionRead = model.OnVisibilityRead = model.OnGesturesRead =
            native.OnPermissionRead = () => throw new InvalidOperationException("Retired getter");
        Assert.False(value.CanEdit); Assert.False(value.CanWrite); Assert.False(value.ShowExpander);
        Assert.Equal(UI.BeginEditGestures.None, value.EditGestures);
        Assert.Throws<ObjectDisposedException>(() => value.Write("retired"));
        Assert.Throws<ObjectDisposedException>(() => value.IsExpanded = true);
        Assert.Equal(0, native.Writes); Assert.Equal(0, native.Disposals); Assert.False(model.IsExpanded);
    }

    [Fact]
    public void Live_gestures_are_not_a_construction_time_snapshot()
    {
        var model = new Expander(new TextProbe("text")); using var value = Adapt(model);
        model.Gestures = UI.BeginEditGestures.None;
        Assert.Equal(UI.BeginEditGestures.None, value.EditGestures);
        model.Gestures = UI.BeginEditGestures.Default;
        Assert.Equal(UI.BeginEditGestures.Default, value.EditGestures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Disposal_attempts_every_owned_cleanup_and_preserves_failure_order(int failures)
    {
        var parentFailure = new InvalidOperationException("parent remove");
        var childFailure = new InvalidOperationException("child remove");
        var disposeFailure = new InvalidOperationException("parent dispose");
        var text = new TextProbe("text"); var model = new Expander(text); var value = Adapt(model);
        var expected = new List<Exception>();
        if ((failures & 1) != 0) { model.RemoveFailure = parentFailure; expected.Add(parentFailure); }
        if ((failures & 2) != 0) { text.RemoveFailure = childFailure; expected.Add(childFailure); }
        if ((failures & 4) != 0) { model.DisposeFailure = disposeFailure; expected.Add(disposeFailure); }
        var error = Record.Exception(value.Dispose);
        if (expected.Count == 0) Assert.Null(error);
        else if (expected.Count == 1) Assert.Same(expected[0], error);
        else Assert.Equal(expected, Assert.IsType<AggregateException>(error).InnerExceptions);
        Assert.Equal(0, model.Subscribers); Assert.Equal(0, text.Subscribers);
        Assert.Equal(1, model.Disposals); Assert.Equal(0, text.Disposals);
        value.Dispose(); Assert.Equal(1, model.Disposals);
    }

    [Fact]
    public void Notification_failure_is_not_replaced_by_reentrant_disposal_failure()
    {
        var primary = new InvalidOperationException("consumer notification"); var cleanup = new InvalidOperationException("parent removal");
        var old = new TextProbe("old"); var current = new TextProbe("current"); var model = new Expander(old);
        var value = Adapt(model); model.RemoveFailure = cleanup;
        value.PropertyChanged += (_, _) => { value.Dispose(); throw primary; };
        var error = Assert.Throws<AggregateException>(() => model.Replace(current));
        Assert.Collection(error.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        Assert.Equal(0, model.Subscribers); Assert.Equal(0, old.Subscribers); Assert.Equal(0, current.Subscribers);
        Assert.Equal(1, model.Disposals); value.Dispose();
    }

    [Fact]
    public void Returning_content_getter_is_ignored_after_disposal()
    {
        var old = new TextProbe("old"); var candidate = new TextProbe("candidate"); var model = new Expander(old);
        using var value = Adapt(model); model.OnContentRead = value.Dispose; model.Replace(candidate);
        Assert.Equal(0, candidate.Subscribers); Assert.Equal(0, old.Subscribers); Assert.Equal(0, model.Subscribers);
        Assert.Equal(1, model.Disposals); Assert.Equal(0, candidate.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cyclic_replacement_is_rejected_and_a_later_valid_replacement_recovers(bool ancestor)
    {
        var text = new TextProbe("text"); var child = new Expander(text); var parent = new Expander(child);
        using var value = Adapt(parent);
        Assert.Throws<InvalidOperationException>(() => child.Replace(ancestor ? parent : child));
        Assert.Equal(1, text.Subscribers); Assert.False(value.CanWrite);
        var current = new TextProbe("current"); child.Replace(current);
        Assert.Equal(0, text.Subscribers); Assert.Equal(1, current.Subscribers); Assert.True(value.CanWrite);
        value.Write("recovered"); Assert.Equal("recovered", current.Text);
    }

    [Fact]
    public void Same_content_notification_preserves_nested_adapter_identity_and_writability()
    {
        var text = new TextProbe("text"); var child = new Expander(text); var parent = new Expander(child);
        using var value = Adapt(parent); var nested = value.Inner;
        parent.NotifyContent(); parent.NotifyContent();
        Assert.Same(nested, value.Inner); Assert.True(value.CanWrite);
        Assert.Equal(1, parent.Subscribers); Assert.Equal(1, child.Subscribers); Assert.Equal(1, text.Subscribers);
        value.Write("nested"); Assert.Equal("nested", text.Text);
    }

    [Fact]
    public void Retired_child_captured_notifications_are_ignored()
    {
        var old = new TextProbe("old"); var model = new Expander(old); using var value = Adapt(model);
        var queued = old.Capture(); model.Replace(new TextProbe("current"));
        var events = 0; value.PropertyChanged += (_, _) => ++events;
        queued?.Invoke(old, new PropertyChangedEventArgs("Value")); Assert.Equal(0, events);
    }

    [Fact]
    public void Warm_native_permission_and_visibility_reads_allocate_no_managed_storage()
    {
        using var value = Adapt(new Expander(new NativeValue()));
        for (var i = 0; i < 1024; ++i) { _ = value.CanEdit; _ = value.CanWrite; _ = value.ShowExpander; _ = value.EditGestures; }
        var accepted = 0; var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
            if (value.CanEdit && value.CanWrite && value.ShowExpander && value.EditGestures == UI.BeginEditGestures.Default) ++accepted;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(4096, accepted); Assert.Equal(0L, allocated);
    }

    private static ExpanderCellValue Adapt(Expander model, bool ownsModel = true) =>
        Assert.IsAssignableFrom<ExpanderCellValue>(CellColumnAdapter<Node>.Adapt(model, ownsModel));
    private class Observable : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs ContentChanged = new("Content");
        private PropertyChangedEventHandler? _handlers;
        internal Action<PropertyChangedEventHandler>? BeforeAdd;
        internal Action<PropertyChangedEventHandler>? AfterAdd;
        internal Action? OnRemove;
        internal Exception? AddFailure;
        internal Exception? RemoveFailure;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                var before = BeforeAdd; BeforeAdd = null; before?.Invoke(value!);
                _handlers += value;
                var after = AfterAdd; AfterAdd = null; after?.Invoke(value!);
                if (AddFailure is { } error) throw error;
            }
            remove
            {
                _handlers -= value; var callback = OnRemove; OnRemove = null; callback?.Invoke();
                if (RemoveFailure is { } error) throw error;
            }
        }
        internal PropertyChangedEventHandler? Capture() => _handlers;
        internal void NotifyContent(bool reset = false) => _handlers?.Invoke(this, reset ? new(null) : ContentChanged);
    }
    private sealed class Node { }
    private sealed class Row : IRow<Node>
    {
        public Node Model { get; } = new();
        object? IRow.Model => Model;
        public object? Header => null;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class Expander(object? content) : Observable, UI.IExpanderCell, IDisposable
    {
        private object? _content = content;
        internal Action? OnContentRead;
        internal Action? OnPermissionRead;
        internal Action? OnVisibilityRead;
        internal Action? OnGesturesRead;
        internal Exception? DisposeFailure;
        internal int Disposals;
        internal UI.BeginEditGestures Gestures = UI.BeginEditGestures.Default;
        public object? Content { get { var result = _content; var callback = OnContentRead; OnContentRead = null; callback?.Invoke(); return result; } }
        public IRow Row { get; } = new Row();
        public object? Value => (_content as UI.ICell)?.Value;
        public bool CanEdit { get { var callback = OnPermissionRead; OnPermissionRead = null; callback?.Invoke(); return true; } }
        public UI.BeginEditGestures EditGestures { get { var result = Gestures; var callback = OnGesturesRead; OnGesturesRead = null; callback?.Invoke(); return result; } }
        public bool IsExpanded { get; set; }
        public bool ShowExpander { get { var callback = OnVisibilityRead; OnVisibilityRead = null; callback?.Invoke(); return true; } }
        internal void Replace(object? content, bool reset = false) { _content = content; NotifyContent(reset); }
        public void Dispose() { ++Disposals; if (DisposeFailure is { } error) throw error; }
    }
    private sealed class TextProbe(string text) : Observable, UI.ITextCell, IDisposable
    {
        internal int Writes; internal int Disposals;
        internal Action? OnAlignmentRead;
        internal Exception? AlignmentFailure;
        private string? _text = text;
        public string? Text { get => _text; set { ++Writes; _text = value; } }
        public object? Value => Text;
        public bool CanEdit => true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public TextAlignment TextAlignment { get { var callback = OnAlignmentRead; OnAlignmentRead = null; callback?.Invoke(); if (AlignmentFailure is { } error) throw error; return TextAlignment.Left; } }
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
        public void Dispose() => ++Disposals;
    }
    private sealed class NativeValue : CellValue
    {
        internal int Writes; internal int Disposals; internal Action? OnPermissionRead;
        public override object? Value => "native";
        public override bool CanEdit { get { var callback = OnPermissionRead; OnPermissionRead = null; callback?.Invoke(); return true; } }
        public override void Write(object? value) => ++Writes;
        public override void Dispose() => ++Disposals;
    }
}
