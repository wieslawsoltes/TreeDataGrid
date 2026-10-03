using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Executes the public observable model through native expander/text controls.</summary>
internal static class PublicExpanderRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        PublicExpanderConstructionChecks.RunAll();
        var previous = page.Content;
        var root = new Node();
        root.Children.Add(new());
        using var source = new HierarchicalTreeDataGridSource<Node>([root]);
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", _ => "Row"), model => model.Children));
        var row = (IExpanderRow<Node>)source.Rows[0];
        using var text = new Values<string>("Public content");
        using var show = new Values<bool>(true);
        using var expanded = new Values<bool>(false);
        using var content = new UI.TextCell<string>(text, false);
        using var model = new UI.ExpanderCell<Node>(content, row, show, expanded);
        var control = new TreeDataGridExpanderCell();
        var host = new Border { Width = 480, Height = 80, Child = control };
        try
        {
            page.Content = host;
            control.Realize(new TreeDataGridElementFactory(), null, model, 0, 0);
            await Task.Delay(100);
            host.UpdateLayout();
            var inner = FindText(control) ?? throw new InvalidOperationException("The public expander did not create a native text control.");
            Check(control.ActualWidth > 0 && inner.ActualHeight > 0 && inner.Value == "Public content" &&
                ReferenceEquals(control.Model, model) && ReferenceEquals(model.Row, source.Rows[0]),
                "The public expander lost native geometry, content or Core row identity.");
            Check(control.ShowExpander && !control.IsExpanded && source.Rows.Count == 1,
                "Initial observable expander state was not applied.");
            expanded.OnNext(true);
            Check(control.IsExpanded && source.Rows.Count == 2, "Observable expansion did not update the native control and Core hierarchy.");
            control.IsExpanded = false;
            Check(!row.IsExpanded && source.Rows.Count == 1, "Native expansion writeback bypassed the shared Core row controller.");
            show.OnNext(false);
            Check(!control.ShowExpander, "Observable expander visibility did not update the native control.");
            show.OnNext(true);
            text.OnNext("Live content");
            Check(inner.Value == "Live content", "A live observable content value did not update the native inner cell.");
            Check(control.BeginEdit(), "The public expander did not delegate editing to its native text cell.");
            control.EditingText = "Committed content";
            Check(control.CommitEdit() && text.Current == "Committed content" && inner.Value == text.Current,
                "Public expander editing did not write to the original observable content.");
            control.Unrealize();
            Check(control.Model is null && show.Subscribers == 1 && expanded.Subscribers == 1 && text.Subscribers == 1,
                "Native unrealization disposed its borrowed public model.");
            model.Dispose();
            Check(show.Subscribers == 0 && expanded.Subscribers == 0 && text.Subscribers == 0,
                "Public model disposal retained observable subscriptions.");
            source.Expand(0);
            Check(source.Rows.Count == 2, "Public cell cleanup retired a caller-owned Core row/source.");
            Exception? retiredWrite = null;
            try { model.IsExpanded = false; }
            catch (Exception error) { retiredWrite = error; }
            Check(retiredWrite is ObjectDisposedException && row.IsExpanded && source.Rows.Count == 2,
                "The disposed public model could still write to the live Core hierarchy.");

            // Reuse the same native control and exact Core row with a new public
            // model. Retired observers must not affect its values or expansion.
            using var replacementText = new Values<string>("Recovered content");
            using var replacementShow = new Values<bool>(true);
            using var replacementExpanded = new Values<bool>(true);
            using var replacementContent = new UI.TextCell<string>(replacementText, false);
            using var replacement = new UI.ExpanderCell<Node>(replacementContent, row, replacementShow, replacementExpanded);
            control.Realize(new TreeDataGridElementFactory(), null, replacement, 0, 0);
            host.UpdateLayout();
            var replacementInner = FindText(control) ?? throw new InvalidOperationException("Recovery lost the native inner control.");
            show.OnNext(false);
            expanded.OnNext(false);
            text.OnNext("Retired content");
            Check(ReferenceEquals(control.Model, replacement) && ReferenceEquals(replacement.Row, row) &&
                control.IsExpanded && control.ShowExpander && replacementInner.Value == "Recovered content" &&
                source.Rows.Count == 2, "Retired sources contaminated a recovered native expander.");
            Check(control.BeginEdit(), "The recovered public expander could not edit.");
            control.EditingText = "Recovered edit";
            Check(control.CommitEdit() && replacementText.Current == "Recovered edit",
                "The recovered expander wrote through the retired content binding.");
            control.IsExpanded = false;
            Check(!row.IsExpanded && source.Rows.Count == 1, "Recovered native expansion lost the Core controller.");
            control.Unrealize();
            replacement.Dispose();
            Check(replacementText.Subscribers == 0 && replacementShow.Subscribers == 0 &&
                replacementExpanded.Subscribers == 0, "The recovered model leaked observers.");
            Console.WriteLine("UNO_RUNTIME_PUBLIC_EXPANDER_RECOVERY_PASSED: retired direct write rejected, " +
                "same native control and Core row, old-source isolation, new editor writeback and exact cleanup");
            Console.WriteLine("UNO_RUNTIME_PUBLIC_EXPANDER_PASSED: native rendering, borrowed Core row, observable expansion/visibility/content, controller writeback, text editing and deterministic ownership cleanup");
        }
        finally
        {
            try { control.Unrealize(); }
            finally { page.Content = previous; }
        }
    }

    private static TreeDataGridTextCell? FindText(DependencyObject root)
    {
        if (root is TreeDataGridTextCell text) return text;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); ++index)
            if (FindText(VisualTreeHelper.GetChild(root, index)) is { } found) return found;
        return null;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Node { public ObservableCollection<Node> Children { get; } = new(); }
    private sealed class Values<T>(T initial) : IObservable<T>, IObserver<T>, IDisposable
    {
        private readonly List<IObserver<T>> _observers = new();
        public T Current { get; private set; } = initial;
        public int Subscribers => _observers.Count;
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            observer.OnNext(Current);
            return new Lease(() => _observers.Remove(observer));
        }
        public void OnNext(T value)
        {
            Current = value;
            foreach (var observer in _observers.ToArray()) observer.OnNext(value);
        }
        public void OnError(Exception error) { foreach (var observer in _observers.ToArray()) observer.OnError(error); }
        public void OnCompleted() { foreach (var observer in _observers.ToArray()) observer.OnCompleted(); }
        public void Dispose() => _observers.Clear();
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() { var release = _release; _release = null; release?.Invoke(); }
    }
}
