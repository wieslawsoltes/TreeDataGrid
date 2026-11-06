using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using A = Avalonia.Controls.Selection;
using C = TreeDataGridCore.Selection;
using UC = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class FlatSelectionParityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(19)] [InlineData(97)]
    [InlineData(101)] [InlineData(127)] [InlineData(257)] [InlineData(1024)] [InlineData(65537)]
    public void Randomized_operations_match_reference_state_events_and_exceptions(int seed) => RunSequence(seed, validOnly: false);

    [Theory]
    [InlineData(1)] [InlineData(19)] [InlineData(101)] [InlineData(257)] [InlineData(65537)]
    public void Valid_input_sequences_keep_both_selected_item_views_readable(int seed) => RunSequence(seed, validOnly: true);

    private void RunSequence(int seed, bool validOnly)
    {
        var left = new ObservableCollection<string>(Enumerable.Range(0, 12).Select(x => "Item " + x));
        var right = new ObservableCollection<string>(left);
        var a = new A.SelectionModel<string>(left);
        var c = new C.SelectionModel<string>(right);
        var aEvents = new List<string>();
        var cEvents = new List<string>();
        a.PropertyChanged += (_, e) => aEvents.Add("P:" + e.PropertyName);
        c.PropertyChanged += (_, e) => cEvents.Add("P:" + e.PropertyName);
        a.SelectionChanged += (_, e) => aEvents.Add("S:" + string.Join(',', e.DeselectedIndexes) + "/" + string.Join(',', e.SelectedIndexes) + "/" + string.Join(',', e.DeselectedItems) + "/" + string.Join(',', e.SelectedItems));
        c.SelectionChanged += (_, e) => cEvents.Add("S:" + string.Join(',', e.DeselectedIndexes) + "/" + string.Join(',', e.SelectedIndexes) + "/" + string.Join(',', e.DeselectedItems) + "/" + string.Join(',', e.SelectedItems));
        a.SourceReset += (_, _) => aEvents.Add("reset");
        c.SourceReset += (_, _) => cEvents.Add("reset");
        var random = new Random(seed);
        var referenceReadFailures = 0;
        try
        {
            for (var step = 0; step < 200; ++step)
            {
                var operation = random.Next(13);
                var first = random.Next(-2, left.Count + 3);
                var last = random.Next(-2, left.Count + 3);
                if (validOnly)
                {
                    first = Math.Clamp(first, 0, Math.Max(0, left.Count - 1));
                    last = Math.Clamp(last, first, Math.Max(first, left.Count - 1));
                    if (left.Count == 0) operation = 9;
                    if (a.SingleSelect && operation is 5 or 8) operation = 0;
                }
                var item = "New " + step;
                output.WriteLine($"seed={seed}; step={step}; valid={validOnly}; op={operation}; first={first}; last={last}; count={left.Count}; single={a.SingleSelect}");
                aEvents.Clear(); cEvents.Clear();
                Exception? leftError = null, rightError = null;
                try { Apply(a, left); } catch (Exception error) { leftError = error; }
                try { ApplyCore(c, right); } catch (Exception error) { rightError = error; }
                Assert.Equal(Failure(leftError), Failure(rightError));
                if (validOnly) { Assert.Null(leftError); Assert.Null(rightError); }
                Assert.Equal(a.SingleSelect, c.SingleSelect);
                Assert.Equal(a.SelectedIndex, c.SelectedIndex);
                Assert.Equal(a.AnchorIndex, c.AnchorIndex);
                Assert.Equal(a.Count, c.Count);
                Assert.Equal(a.SelectedItem, c.SelectedItem);
                Assert.Equal(a.SelectedIndexes.ToArray(), c.SelectedIndexes.ToArray());
                // A differential oracle can itself reject a read after invalid
                // commands. Evaluate BOTH sides before asserting; never omit the
                // target read or turn every exception into a successful value.
                var aItems = ReadItems(() => a.SelectedItems.ToArray());
                var cItems = ReadItems(() => c.SelectedItems.ToArray());
                Assert.Equal(aItems.Failure, cItems.Failure);
                Assert.Equal(aItems.Items, cItems.Items);
                if (validOnly) { Assert.Null(aItems.Failure); Assert.Null(cItems.Failure); }
                if (aItems.Failure is not null)
                {
                    ++referenceReadFailures;
                    output.WriteLine("REFERENCE_READ_FAILURE_MATCHED=" + aItems.Failure);
                }
                Assert.Equal(aEvents, cEvents);
                void Apply(A.SelectionModel<string> model, ObservableCollection<string> items)
                {
                    switch (operation)
                    {
                        case 0: model.Select(first); break;
                        case 1: model.Deselect(first); break;
                        case 2: model.SelectedIndex = first; break;
                        case 3: model.AnchorIndex = first; break;
                        case 4: model.SingleSelect = !model.SingleSelect; break;
                        case 5: model.SelectRange(first, last); break;
                        case 6: model.DeselectRange(first, last); break;
                        case 7: model.Clear(); break;
                        case 8: model.SelectAll(); break;
                        case 9: items.Insert(Math.Clamp(first, 0, items.Count), item); break;
                        case 10: if (items.Count > 0) items.RemoveAt(Math.Clamp(first, 0, items.Count - 1)); break;
                        case 11: if (items.Count > 0) items[Math.Clamp(first, 0, items.Count - 1)] = item; break;
                        case 12: if (items.Count > 0) items.Move(Math.Clamp(first, 0, items.Count - 1), Math.Clamp(last, 0, items.Count - 1)); break;
                    }
                }
                void ApplyCore(C.SelectionModel<string> model, ObservableCollection<string> items)
                {
                    switch (operation)
                    {
                        case 0: model.Select(first); break;
                        case 1: model.Deselect(first); break;
                        case 2: model.SelectedIndex = first; break;
                        case 3: model.AnchorIndex = first; break;
                        case 4: model.SingleSelect = !model.SingleSelect; break;
                        case 5: model.SelectRange(first, last); break;
                        case 6: model.DeselectRange(first, last); break;
                        case 7: model.Clear(); break;
                        case 8: model.SelectAll(); break;
                        case 9: items.Insert(Math.Clamp(first, 0, items.Count), item); break;
                        case 10: if (items.Count > 0) items.RemoveAt(Math.Clamp(first, 0, items.Count - 1)); break;
                        case 11: if (items.Count > 0) items[Math.Clamp(first, 0, items.Count - 1)] = item; break;
                        case 12: if (items.Count > 0) items.Move(Math.Clamp(first, 0, items.Count - 1), Math.Clamp(last, 0, items.Count - 1)); break;
                    }
                }
            }
            output.WriteLine($"REFERENCE_READ_FAILURE_TOTAL={referenceReadFailures}; commands=200");
        }
        finally { a.Source = null; c.Source = null; }
    }
    private static string? Failure(Exception? error) => error is null ? null :
        error.GetType().FullName + (error is ArgumentException argument ? ":" + argument.ParamName : "");
    private static (string?[]? Items, string? Failure) ReadItems(Func<string?[]> read)
    {
        try { return (read(), null); }
        catch (Exception error) { return (null, Failure(error)); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Deferred_item_selection_and_source_replacement_match(bool multiple)
    {
        var a = new A.SelectionModel<string> { SingleSelect = !multiple, SelectedItem = "second" };
        var c = new C.SelectionModel<string> { SingleSelect = !multiple, SelectedItem = "second" };
        Assert.Equal(a.SelectedItem, c.SelectedItem);
        a.Source = new[] { "first", "second", "third" };
        c.Source = new[] { "first", "second", "third" };
        Assert.Equal(a.SelectedIndex, c.SelectedIndex);
        Assert.Equal(a.SelectedItems.ToArray(), c.SelectedItems.ToArray());
        a.Source = null; c.Source = null;
        Assert.Equal(a.SelectedIndex, c.SelectedIndex);
        a.Source = new[] { "only" }; c.Source = new[] { "only" };
        Assert.Equal(a.SelectedIndex, c.SelectedIndex);
        Assert.Equal(a.SelectedItems.ToArray(), c.SelectedItems.ToArray());
        a.Source = null; c.Source = null;
    }

    [Fact]
    public void Nested_batches_and_lost_selection_recovery_match()
    {
        var a = new A.SelectionModel<string>(new[] { "first", "second", "third" }) { SingleSelect = false };
        var c = new C.SelectionModel<string>(new[] { "first", "second", "third" }) { SingleSelect = false };
        var aCount = 0; var cCount = 0;
        a.SelectionChanged += (_, _) => ++aCount;
        c.SelectionChanged += (_, _) => ++cCount;
        a.BeginBatchUpdate(); c.BeginBatchUpdate();
        a.Select(0); c.Select(0);
        a.BeginBatchUpdate(); c.BeginBatchUpdate();
        a.Select(2); c.Select(2);
        a.EndBatchUpdate(); c.EndBatchUpdate();
        Assert.Equal(0, aCount); Assert.Equal(0, cCount);
        a.EndBatchUpdate(); c.EndBatchUpdate();
        Assert.Equal(aCount, cCount);
        Assert.Equal(a.SelectedIndexes.ToArray(), c.SelectedIndexes.ToArray());
        a.LostSelection += (_, _) => a.Select(1);
        c.LostSelection += (_, _) => c.Select(1);
        a.Clear(); c.Clear();
        Assert.Equal(a.SelectedIndexes.ToArray(), c.SelectedIndexes.ToArray());
        Assert.Equal(1, c.SelectedIndex);
        Assert.Throws<InvalidOperationException>(c.EndBatchUpdate);
        a.Source = null; c.Source = null;
    }

    [Fact]
    public void Selecting_one_million_items_keeps_range_based_storage()
    {
        var source = new int[1_000_000];
        var model = new C.SelectionModel<int>(source) { SingleSelect = false };
        model.SelectAll(); model.Clear();
        var before = GC.GetAllocatedBytesForCurrentThread();
        model.SelectAll();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(source.Length, model.Count);
        Assert.Equal(source.Length - 1, model.SelectedIndexes[source.Length - 1]);
        Assert.True(allocated < 64 * 1024, $"SelectAll allocated {allocated} bytes; expected range storage.");
        model.Source = null;
    }

    [Fact]
    public void Native_column_selection_uses_actual_view_columns_and_shared_Core()
    {
        var columns = new UC.ColumnList<Item>();
        using var first = new UC.TextColumn<Item, string>("First", x => x.Name);
        using var second = new UC.TextColumn<Item, string>("Second", x => x.Name);
        columns.Add(first); columns.Add(second);
        var model = new Uno.Controls.Selection.TreeDataGridColumnSelectionModel(columns) { SingleSelect = false };
        Uno.Controls.Selection.ITreeDataGridColumnSelectionModel contract = model;
        Assert.IsAssignableFrom<C.ISelectionModel>(contract);
        Assert.Same(columns, model.Source);
        contract.SelectRange(0, 1);
        Assert.Same(first, contract.SelectedItem);
        Assert.Same(second, contract.SelectedItems[1]);
        contract.SelectedItem = second;
        Assert.Equal(1, contract.SelectedIndex);
        Assert.Same(second, contract.SelectedItem);
        columns.RemoveAt(0);
        Assert.Equal(0, contract.SelectedIndex);
        Assert.Same(second, contract.SelectedItem);
        model.Source = null;
    }
    private sealed record Item(string Name);
}
