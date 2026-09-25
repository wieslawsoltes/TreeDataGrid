using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Dynamic custom content through real native controls and the shared Core hierarchy.</summary>
internal static class CustomExpanderLifetimeRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var root = new Node(); root.Children.Add(new());
        using var source = new HierarchicalTreeDataGridSource<Node>([root]);
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(new TextColumn<Node, string>("Name", _ => "Root"), x => x.Children));
        var row = (IExpanderRow<Node>)source.Rows[0];
        var first = new TextModel("First"); var winner = new TextModel("Winner"); var obsolete = new TextModel("Obsolete");
        var model = new Expander(row, first);
        var control = new TreeDataGridExpanderCell();
        var host = new Border { Width = 500, Height = 100, Child = control };
        try
        {
            page.Content = host;
            control.Realize(new TreeDataGridElementFactory(), null, model, 0, 0);
            await Settle();
            var adapter = (ExpanderCellValue)control.ViewModel!;
            Check(Text().Value == "First" && control.ActualWidth > 0 && Text().ActualHeight > 0 &&
                ReferenceEquals(adapter.Row, source.Rows[0]) && ReferenceEquals(Text().RowModel, root),
                "Initial custom expander lost native rendering or shared Core identity.");
            var changes = 0;
            adapter.PropertyChanged += (_, args) => { if (args is CellContentChangedEventArgs) ++changes; };
            obsolete.ReadAlignment = () => model.Replace(winner);
            model.Replace(obsolete);
            await Settle();
            Check(ReferenceEquals(adapter.Content, winner) && Text().Value == "Winner" && changes == 1,
                "A returning custom content candidate overwrote a newer native value or published twice.");
            Check(first.Subscribers == 0 && obsolete.Subscribers == 0 && winner.Subscribers == 1,
                "Reentrant content replacement retained a retired subscription.");
            Check(control.BeginEdit(), "The replacement custom text did not open its native editor.");
            control.EditingText = "Native commit";
            Check(control.CommitEdit() && winner.Text == "Native commit" && winner.Writes == 1 && Text().Value == winner.Text,
                "Native editing did not commit once to the current custom content.");

            var failed = new TextModel("Failed") { AddFailure = new InvalidOperationException("Expected candidate attachment failure.") };
            Check(ReferenceEquals(Capture(() => model.Replace(failed)), failed.AddFailure),
                "A failed content attachment lost the application exception.");
            await Settle();
            Check(!adapter.CanWrite && !adapter.Inner.CanWrite && !control.BeginEdit() && failed.Subscribers == 0,
                "Failed synchronization left an old native editor or adapter writable.");
            Check(Capture(() => adapter.Inner.Write("Old target")) is InvalidOperationException && winner.Writes == 1,
                "The failed candidate allowed a write through the former inner adapter.");
            failed.AddFailure = null; model.NotifyContent(); await Settle();
            Check(Text().Value == "Failed" && winner.Subscribers == 0 && failed.Subscribers == 1,
                "A failed custom content attachment could not recover cleanly.");

            var nestedText = new TextModel("Nested"); var nested = new Expander(row, nestedText);
            model.Replace(nested); await Settle();
            Check(Text().Value == "Nested" && nested.Subscribers == 1 && nestedText.Subscribers == 1,
                "Nested custom content failed to create a native text descendant.");
            Check(Capture(() => nested.Replace(model)) is InvalidOperationException,
                "A dynamic ancestor cycle was accepted.");
            await Settle();
            Check(!control.BeginEdit(), "A rejected nested cycle left a stale native editor.");
            nested.Replace(nestedText); await Settle();
            Check(control.BeginEdit(), "A nested expander did not recover its native editor after cycle rejection.");
            control.EditingText = "Nested commit";
            Check(control.CommitEdit() && nestedText.Text == "Nested commit", "Nested edit delegation lost writeback.");

            using var check = new UI.CheckBoxCell(true);
            model.Replace(check); await Settle();
            var checkControl = ShowcaseRuntimeChecks.Descendants(control).OfType<TreeDataGridCheckBoxCell>().FirstOrDefault();
            Check(checkControl is { Value: true, IsReadOnly: true } && !control.BeginEdit() &&
                nested.Subscribers == 0 && nestedText.Subscribers == 0,
                "Replacing custom text with checkbox content retained stale controls or subscriptions.");
            model.Replace(null); await Settle();
            Check(!ShowcaseRuntimeChecks.Descendants(control).OfType<TreeDataGridCell>().Any(x => x.Model is not null) &&
                !control.BeginEdit(), "Empty content retained a realized editor.");
            model.Replace(winner); await Settle();
            control.IsExpanded = true;
            Check(row.IsExpanded && source.Rows.Count == 2, "Custom expansion bypassed the Core hierarchy controller.");
            control.IsExpanded = false;
            Check(source.Rows.Count == 1, "Custom collapse did not reach the Core hierarchy.");

            var returning = new TextModel("Returning after retirement");
            model.ReadContent = control.Unrealize;
            model.Replace(returning);
            await Settle();
            Check(control.Model is null && model.Subscribers == 0 && winner.Subscribers == 0 && returning.Subscribers == 0,
                "Retirement during content lookup left subscriptions or a native model.");
            Check(model.Disposals == 0 && first.Disposals == 0 && winner.Disposals == 0 && nested.Disposals == 0 &&
                nestedText.Disposals == 0 && failed.Disposals == 0 && obsolete.Disposals == 0,
                "Native unrealization disposed a borrowed public custom model.");
            source.Expand(0);
            Check(source.Rows.Count == 2, "Custom adapter retirement disposed or changed the caller-owned Core source.");
            Console.WriteLine("UNO_RUNTIME_CUSTOM_EXPANDER_LIFETIME_PASSED: native dynamic content, reentrant metadata, stable value events, edit commit, failed attachment disables old editor, recovery, nested cycle rejection, checkbox/empty transitions, Core expansion and borrowed cleanup");
        }
        finally
        {
            try { control.Unrealize(); }
            finally { page.Content = previous; }
        }
        TreeDataGridTextCell Text() => ShowcaseRuntimeChecks.Descendants(control).OfType<TreeDataGridTextCell>().FirstOrDefault() ??
            throw new InvalidOperationException("No realized native text descendant.");
        async Task Settle() { await Task.Delay(80); host.UpdateLayout(); }
    }
    private static Exception? Capture(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Node { public ObservableCollection<Node> Children { get; } = new(); }
    private abstract class Observable : INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? _handlers;
        internal Exception? AddFailure;
        internal int Disposals;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _handlers += value; if (AddFailure is { } error) throw error; }
            remove => _handlers -= value;
        }
        protected void Notify(string name) => _handlers?.Invoke(this, new(name));
        public void Dispose() => ++Disposals;
    }
    private sealed class Expander(IExpanderRow<Node> row, object? content) : Observable, UI.IExpanderCell
    {
        private object? _content = content;
        internal Action? ReadContent;
        public IRow Row => row;
        public object? Content { get { var result = _content; var callback = ReadContent; ReadContent = null; callback?.Invoke(); return result; } }
        public object? Value => _content is UI.IExpanderCellPresentation ? null : (_content as UI.ICell)?.Value;
        public bool CanEdit => _content is UI.IExpanderCellPresentation ? true : (_content as UI.ICell)?.CanEdit == true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public bool IsExpanded { get => row.IsExpanded; set { row.IsExpanded = value; Notify(nameof(IsExpanded)); } }
        public bool ShowExpander => row.ShowExpander;
        internal void Replace(object? value) { _content = value; NotifyContent(); }
        internal void NotifyContent() => Notify(nameof(Content));
    }
    private sealed class TextModel(string text) : Observable, UI.ITextCell
    {
        private string? _text = text;
        internal Action? ReadAlignment;
        internal int Writes;
        public object? Value => Text;
        public bool CanEdit => true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public string? Text { get => _text; set { _text = value; ++Writes; Notify(nameof(Value)); } }
        public TextAlignment TextAlignment { get { var callback = ReadAlignment; ReadAlignment = null; callback?.Invoke(); return TextAlignment.Left; } }
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
    }
}
