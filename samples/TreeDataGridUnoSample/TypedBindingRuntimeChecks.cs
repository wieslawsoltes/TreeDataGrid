using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Data;
using Uno.Experimental.Data;

namespace TreeDataGridUnoSample;

/// <summary>Native typed bindings through public properties and real dependency-property callbacks.</summary>
internal static class TypedBindingRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        ExplicitSource();
        ContextReplacement();
        TwoWay();
        OneTime();
        SourceOnly();
        Replacement();
        ReentrantReplacement();
        await ParentContextAsync(page);
        Console.WriteLine("UNO_RUNTIME_TYPED_BINDING_PASSED: cases=8; explicit/context/parent roots, fallback, two-way and source-only native writeback, one-time observation, cross-generic replacement, reentrant installation and exact subscription cleanup");
    }

    private static void ExplicitSource()
    {
        var row = new Model { Text = "First" };
        var text = new TextBlock();
        var descriptor = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        descriptor.Source = row;
        using var binding = descriptor.Bind(text, TextBlock.TextProperty);
        Check(text.Text == "First" && row.Subscribers == 1, "Explicit typed source did not initialize.");
        row.Text = "Second";
        Check(text.Text == "Second", "Explicit source change did not reach the native target.");
        binding.Dispose();
        Check(row.Subscribers == 0, "Disposing a target binding retained its source subscription.");
        row.Text = "After disposal";
        Check(text.Text == "Second", "A disposed binding changed the target's last local value.");
    }

    private static void ContextReplacement()
    {
        var first = new Model { Text = "First" };
        var second = new Model { Text = "Second" };
        var text = new TextBlock { DataContext = first };
        var descriptor = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        descriptor.FallbackValue = "No model";
        using var binding = descriptor.Bind(text, TextBlock.TextProperty);
        Check(text.Text == "First", "DataContext root did not initialize.");
        text.DataContext = second;
        Check(text.Text == "Second" && first.Subscribers == 0 && second.Subscribers == 1,
            "Replacing DataContext retained the wrong root or subscription.");
        text.DataContext = null;
        Check(text.Text == "No model" && second.Subscribers == 0, "A null root did not publish fallback and release its model.");
        text.DataContext = first;
        Check(text.Text == "First" && first.Subscribers == 1, "A null DataContext did not recover.");
        binding.Dispose();
        Check(first.Subscribers == 0, "Recovered DataContext stayed observed after disposal.");
    }

    private static void TwoWay()
    {
        var row = new Model { Text = "Source" };
        var editor = new TextBox { DataContext = row };
        var writes = 0;
        var descriptor = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        descriptor.Mode = BindingMode.TwoWay;
        descriptor.Write = (model, value) => { ++writes; model.Text = value; };
        using var binding = descriptor.Bind(editor, TextBox.TextProperty);
        Check(editor.Text == "Source" && writes == 0, "Initial source publication echoed through native writeback.");
        editor.Text = "Editor";
        Check(row.Text == "Editor" && writes == 1, "Actual native TextBox changes did not write once.");
        row.Text = "External";
        Check(editor.Text == "External" && writes == 1, "Source updates recursively echoed through the setter.");
        binding.Dispose();
        editor.Text = "Detached";
        Check(row.Text == "External" && writes == 1 && row.Subscribers == 0, "Target callbacks survived disposal.");
    }

    private static void OneTime()
    {
        var row = new Model { Text = "Initial" };
        var text = new TextBlock { DataContext = row };
        var descriptor = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        descriptor.Mode = BindingMode.OneTime;
        using var binding = descriptor.Bind(text, TextBlock.TextProperty);
        Check(text.Text == "Initial" && row.Subscribers == 0, "One-time binding subscribed to model notifications.");
        row.Text = "Ignored";
        Check(text.Text == "Initial", "One-time binding observed a model change.");
    }

    private static void SourceOnly()
    {
        var first = new Model { Text = "Source" };
        var second = new Model { Text = "Other" };
        var editor = new TextBox { Text = "Target", DataContext = first };
        var writes = 0;
        var descriptor = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        descriptor.Mode = BindingMode.OneWayToSource;
        descriptor.Write = (model, value) => { ++writes; model.Text = value; };
        using var binding = descriptor.Bind(editor, TextBox.TextProperty);
        Check(first.Text == "Target" && editor.Text == "Target" && writes == 1, "Source-only mode lost initial target precedence.");
        first.Text = "External";
        Check(editor.Text == "Target", "Source-only mode published a model value to the target.");
        editor.Text = "Changed target";
        Check(first.Text == "Changed target" && writes == 2, "Source-only target callback did not write.");
        editor.DataContext = second;
        Check(second.Text == "Changed target" && first.Subscribers == 0 && writes == 3,
            "Replacement source did not receive the current target value.");
        binding.Dispose();
        Check(second.Subscribers == 0, "Source-only binding leaked the replacement model.");
    }

    private static void Replacement()
    {
        var first = new Model { Text = "First" };
        var second = new OtherModel { Text = "Second" };
        var text = new TextBlock();
        var a = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        a.Source = first;
        var b = TypedBinding<OtherModel>.OneWay(static x => x.Text, [static x => x]);
        b.Source = second;
        using var oldBinding = a.Bind(text, TextBlock.TextProperty);
        using var currentBinding = b.Bind(text, TextBlock.TextProperty);
        Check(text.Text == "Second" && first.Subscribers == 0 && second.Subscribers == 1,
            "Cross-generic replacement did not retire the previous typed binding.");
        oldBinding.Dispose();
        second.Text = "Current";
        first.Text = "Retired";
        Check(text.Text == "Current", "An obsolete lifetime handle disturbed its replacement.");
        currentBinding.Dispose();
        Check(second.Subscribers == 0, "Cross-generic replacement retained a handler.");
    }

    private static void ReentrantReplacement()
    {
        var first = new Model { Text = "First" };
        var second = new Model { Text = "Second" };
        var text = new TextBlock();
        var a = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        var b = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]);
        a.Source = first;
        b.Source = second;
        IDisposable? replacement = null;
        var installed = false;
        var token = text.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) =>
        {
            if (installed) return;
            installed = true;
            replacement = b.Bind(text, TextBlock.TextProperty);
        });
        try
        {
            using var original = a.Bind(text, TextBlock.TextProperty);
            Check(installed && text.Text == "Second" && first.Subscribers == 0 && second.Subscribers == 1,
                "Reentrant initial target publication retained or republished the retired binding.");
            first.Text = "Old";
            Check(text.Text == "Second", "A retired source changed the replacement target.");
            second.Text = "Current";
            Check(text.Text == "Current", "The reentrant replacement did not remain functional.");
        }
        finally
        {
            text.UnregisterPropertyChangedCallback(TextBlock.TextProperty, token);
            replacement?.Dispose();
        }
        Check(first.Subscribers == 0 && second.Subscribers == 0, "Reentrant target installation leaked handlers.");
    }

    private static async Task ParentContextAsync(MainPage page)
    {
        var previous = page.Content;
        var childModel = new Model { Text = "Child" };
        var root = new Model { Child = childModel };
        var parent = new StackPanel { DataContext = root };
        var child = new Border();
        parent.Children.Add(child);
        var descriptor = TypedBinding<Model>.OneWay<Model?>(static x => x.Child, [static x => x]);
        using var binding = descriptor.Bind(child, FrameworkElement.DataContextProperty);
        try
        {
            page.Content = parent;
            await Task.Delay(100);
            parent.UpdateLayout();
            Check(ReferenceEquals(child.DataContext, childModel), "A DataContext binding did not use its visual parent's root.");
            root.Child = null;
            Check(child.DataContext is null, "An explicit null child did not clear the native DataContext.");
            var nextChild = new Model { Text = "Replacement child" };
            var nextRoot = new Model { Child = nextChild };
            parent.DataContext = nextRoot;
            Check(ReferenceEquals(child.DataContext, nextChild) && root.Subscribers == 0,
                "Parent root replacement retained the old root or child DataContext.");
            binding.Dispose();
            Check(nextRoot.Subscribers == 0, "Disposing parent-DataContext binding retained its root.");
        }
        finally { page.Content = previous; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private class Model : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private string _text = string.Empty;
        private Model? _child;
        public string Text { get => _text; set { _text = value; _changed?.Invoke(this, new(nameof(Text))); } }
        public Model? Child { get => _child; set { _child = value; _changed?.Invoke(this, new(nameof(Child))); } }
        internal int Subscribers;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class OtherModel : Model { }
}
