using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

internal static class CellRenderingRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var resources = new CellRenderingResources();
        var template = (ControlTemplate)resources["TextCellTemplate"];
        var contentTemplate = (DataTemplate)resources["ContentTemplate"];
        var factory = new TreeDataGridElementFactory();
        var value = new TestValue();
        var cell = new RenderProbe { Template = template };
        var previous = page.Content;
        var host = new Border { Child = cell, Width = 300, Height = 80 };
        try
        {
            page.Content = host;
            cell.Realize(factory, null, value, 0, 0);
            await Task.Delay(75);
            host.UpdateLayout();
            var text = FindText(cell) ?? throw new InvalidOperationException("Compiled cell template has no native text part.");
            Check(text.Text == "Initial", "The probe did not render its initial model.");

            cell.Reading = () =>
            {
                cell.Reading = null;
                cell.Unrealize();
                return "Retired getter";
            };
            cell.Refresh();
            Check(text.Text == string.Empty && cell.Visibility == Visibility.Collapsed, "A retired getter repainted a cleared cell.");

            cell.Realize(factory, null, value, 0, 0);
            cell.Reading = () =>
            {
                cell.Reading = null;
                cell.Unrealize();
                value.Text = "Replacement";
                cell.Realize(factory, null, value, 0, 0);
                return "Stale same-model getter";
            };
            cell.Refresh();
            Check(text.Text == "Replacement" && ReferenceEquals(cell.Model, value), "Same-model recycling accepted stale rendered text.");

            cell.Reading = () =>
            {
                cell.Reading = null;
                value.Text = "Nested refresh";
                cell.Refresh();
                return "Older refresh";
            };
            cell.Refresh();
            Check(text.Text == "Nested refresh", "An older same-realization refresh overwrote its nested refresh.");

            var failure = new InvalidOperationException("display getter");
            cell.Reading = () => throw failure;
            Check(ReferenceEquals(Capture(cell.Refresh), failure), "A display getter failure lost its identity.");
            cell.Reading = null;
            value.Text = "Recovered";
            cell.Refresh();
            Check(text.Text == "Recovered", "A failed display getter prevented the next refresh.");

            // The guarded render dispatch itself must remain allocation-free
            // when repeatedly publishing an unchanged, already-owned string.
            for (var i = 0; i < 1024; ++i) cell.Refresh();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4096; ++i) cell.Refresh();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, $"Unchanged native render dispatch allocated {allocated} bytes.");
        }
        finally { cell.Reading = null; cell.Unrealize(); host.Child = null; page.Content = previous; }

        VerifyWriteBoundary(false);
        VerifyWriteBoundary(true);
        VerifyTextRefresh(false);
        VerifyTextRefresh(true);
        VerifyTemplateRefresh(contentTemplate, false);
        VerifyTemplateRefresh(contentTemplate, true);
        VerifyTemplateRetirement(contentTemplate);
        Console.WriteLine("UNO_RUNTIME_CELL_RENDERING_PASSED: cases=12; retired/same-model/nested rendering, getter errors, zero warmed render allocation, checkbox write ownership, specialized text/template generations and transactional template context cleanup");
    }

    private static void VerifyWriteBoundary(bool sameModel)
    {
        var factory = new TreeDataGridElementFactory();
        var original = new TestValue();
        var replacement = sameModel ? original : new TestValue();
        var cell = new RenderProbe();
        cell.Realize(factory, null, original, 0, 0);
        try
        {
            original.CheckingWrite = () =>
            {
                original.CheckingWrite = null;
                cell.Unrealize();
                cell.Realize(factory, null, replacement, 0, 0);
            };
            cell.WriteCheckbox(true);
            Check(original.Writes == 0 && replacement.Writes == 0, "A retired CanWrite result wrote a checkbox value.");
            cell.WriteCheckbox(false);
            Check(replacement.Writes == 1, "The current model could not receive a normal checkbox write.");
        }
        finally { cell.Unrealize(); }
    }

    private static void VerifyTextRefresh(bool nestedOnly)
    {
        var factory = new TreeDataGridElementFactory();
        var model = new TextValue();
        var cell = new TextProbe();
        cell.Realize(factory, null, model, 0, 0);
        try
        {
            model.Reading = () =>
            {
                model.Reading = null;
                model.Content = "New text";
                if (nestedOnly) cell.Refresh();
                else { cell.Unrealize(); cell.Realize(factory, null, model, 0, 0); }
            };
            cell.Refresh();
            Check(cell.Value == "New text" && ReferenceEquals(cell.Model, model), "An obsolete ITextCell getter overwrote the latest text facade.");
        }
        finally { model.Reading = null; cell.Unrealize(); }
    }

    private static void VerifyTemplateRefresh(DataTemplate template, bool nestedOnly)
    {
        var factory = new TreeDataGridElementFactory();
        var model = new ContentValue(template);
        var cell = new TemplateProbe();
        cell.Realize(factory, null, model, 0, 0);
        try
        {
            var next = new object();
            model.Reading = () =>
            {
                model.Reading = null;
                model.Content = next;
                if (nestedOnly) cell.Refresh();
                else { cell.Unrealize(); cell.Realize(factory, null, model, 0, 0); }
            };
            cell.Refresh();
            Check(ReferenceEquals(cell.Content, next) && ReferenceEquals(cell.DataContext, model), "An obsolete template getter overwrote current content.");
        }
        finally { model.Reading = null; cell.Unrealize(); }
    }

    private static void VerifyTemplateRetirement(DataTemplate template)
    {
        var factory = new TreeDataGridElementFactory();
        var model = new ContentValue(template);
        var cell = new TemplateProbe();
        var first = new InvalidOperationException("content cleanup");
        var second = new InvalidOperationException("context cleanup");
        cell.Realize(factory, null, model, 0, 0);
        var contentToken = cell.RegisterPropertyChangedCallback(TreeDataGridTemplateCell.ContentProperty, (_, _) =>
        {
            if (cell.Content is null) throw first;
        });
        var contextToken = cell.RegisterPropertyChangedCallback(FrameworkElement.DataContextProperty, (_, _) =>
        {
            if (cell.DataContext is not null) return;
            cell.Unrealize();
            Check(Capture(() => cell.Realize(factory, null, model, 1, 1)) is InvalidOperationException,
                "Template context cleanup admitted a new realization.");
            throw second;
        });
        try
        {
            var failure = Capture(cell.Unrealize) as AggregateException;
            Check(failure is not null && failure.InnerExceptions.SequenceEqual(new[] { first, second }),
                "Template cleanup lost or reordered simultaneous failures.");
            Check(cell.Content is null && cell.DataContext is null && cell.Model is null && cell.Visibility == Visibility.Collapsed,
                "Template retirement retained content/context or visibility.");
        }
        finally
        {
            cell.UnregisterPropertyChangedCallback(TreeDataGridTemplateCell.ContentProperty, contentToken);
            cell.UnregisterPropertyChangedCallback(FrameworkElement.DataContextProperty, contextToken);
            cell.Unrealize();
        }
        cell.Realize(factory, null, model, 1, 1);
        Check(ReferenceEquals(cell.Content, model.Content), "Template cell did not recover after cleanup failures.");
        cell.Unrealize();
    }

    private static TextBlock? FindText(DependencyObject root)
    {
        if (root is TextBlock text) return text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            if (FindText(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
    private static Exception? Capture(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed partial class RenderProbe : TreeDataGridCell
    {
        internal Func<string?>? Reading;
        protected override string? DisplayText => Reading is { } read ? read() : base.DisplayText;
        internal void Refresh() => base.UpdateValue();
        internal void WriteCheckbox(bool? next) => base.OnCheckBoxValueChanged(next);
    }
    private sealed partial class TextProbe : TreeDataGridTextCell { internal void Refresh() => base.UpdateValue(); }
    private sealed partial class TemplateProbe : TreeDataGridTemplateCell { internal void Refresh() => base.UpdateValue(); }
    private sealed class TestValue : CellValue
    {
        internal string Text = "Initial";
        internal Action? CheckingWrite;
        internal int Writes;
        public override object? Value => Text;
        public override bool CanEdit => false;
        public override bool CanWrite { get { CheckingWrite?.Invoke(); return true; } }
        public override void Write(object? value) => ++Writes;
    }
    private sealed class TextValue : CellValue, ITextCell
    {
        internal string Content = "Old text";
        internal Action? Reading;
        public string? Text { get { var value = Content; Reading?.Invoke(); return value; } set => Content = value ?? string.Empty; }
        public override object? Value => Content;
        public override bool CanEdit => false;
        public TextAlignment TextAlignment => TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
        public override void Write(object? value) => throw new NotSupportedException();
    }
    private sealed class ContentValue(DataTemplate template) : CellValue
    {
        internal object Content = new();
        internal Action? Reading;
        public override CellKind ContentKind => CellKind.Template;
        public override object? Value { get { var value = Content; Reading?.Invoke(); return value; } }
        public override bool CanEdit => false;
        public override DataTemplate? GetCellTemplate(Control anchor) => template;
        public override void Write(object? value) => throw new NotSupportedException();
    }
}
