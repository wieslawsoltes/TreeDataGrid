using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Actual native content layout across virtual getters and DP callbacks.</summary>
internal static class CellContentLayoutRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var template = (ControlTemplate)new CellRenderingResources()["TextCellTemplate"];
        var factory = new TreeDataGridElementFactory();
        var value = new TestValue();
        var cell = new Probe { Template = template };
        var host = new Border { Child = cell, Width = 300, Height = 80 };
        var previous = page.Content;
        var cases = 0;
        try
        {
            page.Content = host;
            cell.Realize(factory, null, value, 0, 0);
            await Task.Delay(75);
            host.UpdateLayout();
            var text = FindText(cell) ?? throw new InvalidOperationException("Compiled text part is missing.");
            for (var property = 0; property < 3; ++property)
            {
                VerifyStyle(property, recycle: false);
                VerifyStyle(property, recycle: true);
            }
            Reset();
            var armed = true;
            var token = text.RegisterPropertyChangedCallback(TextBlock.TextAlignmentProperty, (_, _) =>
            {
                if (!armed) return;
                armed = false;
                SetNewStyle();
                cell.Refresh();
            });
            try
            {
                cell.Alignment = TextAlignment.Right;
                cell.Refresh();
                Check(!armed, "The native style callback was not executed.");
                AssertNewStyle();
                ++cases;
            }
            finally { text.UnregisterPropertyChangedCallback(TextBlock.TextAlignmentProperty, token); }

            Reset();
            cell.InnerRead = () =>
            {
                cell.InnerRead = null;
                SetNewStyle();
                cell.Refresh();
                return true;
            };
            cell.Refresh();
            AssertNewStyle();
            Check(text.Visibility == Visibility.Visible, "An obsolete inner-cell mode hid the current text.");
            ++cases;

            Reset();
            var failure = new InvalidOperationException("Style getter failed");
            cell.AlignmentRead = () => throw failure;
            Check(ReferenceEquals(failure, Capture(cell.Refresh)), "A style getter error lost its identity.");
            cell.AlignmentRead = null;
            SetNewStyle();
            cell.Refresh();
            AssertNewStyle();
            ++cases;

            Reset();
            cell.Unrealize();
            value.KindRead = () =>
            {
                value.KindRead = null;
                cell.Unrealize();
                return CellKind.Template;
            };
            Check(Capture(() => cell.Realize(factory, null, value, 0, 0)) is null,
                "Retired content-kind metadata continued into obsolete template validation.");
            Check(cell.Model is null && cell.RowIndex == -1 && cell.Visibility == Visibility.Collapsed,
                "A content-kind callback left its retired realization alive.");
            cell.Realize(factory, null, value, 0, 0);
            Check(ReferenceEquals(cell.Model, value) && text.Text == value.Text, "Content-kind cancellation prevented reuse.");
            ++cases;

            Reset();
            for (var iteration = 0; iteration < 1024; ++iteration) cell.Refresh();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 4096; ++iteration) cell.Refresh();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, $"Unchanged native content layout allocated {allocated} bytes.");
            Check(text.Text == value.Text && text.Visibility == Visibility.Visible,
                "Allocation optimization changed native content or visibility.");
            ++cases;
            Check(cases == 11, "A content-layout scenario was skipped.");
            Console.WriteLine("UNO_RUNTIME_CELL_CONTENT_LAYOUT_PASSED: cases=11; all three text-style getters, same-model recycling, nested refreshes, native setter reentrancy, inner-cell mode, getter failure/recovery, content-kind retirement and zero warm full-layout allocation");

            void SetNewStyle()
            {
                cell.Alignment = TextAlignment.Center;
                cell.Wrapping = TextWrapping.Wrap;
                cell.Trimming = TextTrimming.CharacterEllipsis;
            }
            void AssertNewStyle() => Check(text.TextAlignment == TextAlignment.Center &&
                text.TextWrapping == TextWrapping.Wrap && text.TextTrimming == TextTrimming.CharacterEllipsis &&
                ReferenceEquals(cell.Model, value) && cell.RowIndex == 0,
                "Older content layout overwrote a newer style or realization.");
            void VerifyStyle(int property, bool recycle)
            {
                Reset();
                void Supersede()
                {
                    cell.AlignmentRead = null;
                    cell.WrappingRead = null;
                    cell.TrimmingRead = null;
                    SetNewStyle();
                    if (recycle)
                    {
                        cell.Unrealize();
                        cell.Realize(factory, null, value, 0, 0);
                    }
                    else cell.Refresh();
                }
                if (property == 0) cell.AlignmentRead = () => { Supersede(); return TextAlignment.Right; };
                else if (property == 1) cell.WrappingRead = () => { Supersede(); return TextWrapping.NoWrap; };
                else cell.TrimmingRead = () => { Supersede(); return TextTrimming.None; };
                cell.Refresh();
                AssertNewStyle();
                ++cases;
            }
            void Reset()
            {
                cell.ClearCallbacks();
                value.KindRead = null;
                cell.Unrealize();
                cell.Alignment = TextAlignment.Left;
                cell.Wrapping = TextWrapping.NoWrap;
                cell.Trimming = TextTrimming.None;
                cell.Realize(factory, null, value, 0, 0);
            }
        }
        finally
        {
            cell.ClearCallbacks();
            value.KindRead = null;
            try { cell.Unrealize(); }
            finally { host.Child = null; page.Content = previous; }
        }
    }

    private static Exception? Capture(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static TextBlock? FindText(DependencyObject root)
    {
        if (root is TextBlock text) return text;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); ++index)
            if (FindText(VisualTreeHelper.GetChild(root, index)) is { } found) return found;
        return null;
    }
    private sealed partial class Probe : TreeDataGridCell
    {
        internal TextAlignment Alignment;
        internal TextWrapping Wrapping;
        internal TextTrimming Trimming;
        internal Func<TextAlignment>? AlignmentRead;
        internal Func<TextWrapping>? WrappingRead;
        internal Func<TextTrimming>? TrimmingRead;
        internal Func<bool>? InnerRead;
        protected override TextAlignment DisplayTextAlignment => AlignmentRead?.Invoke() ?? Alignment;
        protected override TextWrapping DisplayTextWrapping => WrappingRead?.Invoke() ?? Wrapping;
        protected override TextTrimming DisplayTextTrimming => TrimmingRead?.Invoke() ?? Trimming;
        protected override bool UsesInnerCellControl => InnerRead?.Invoke() ?? false;
        internal void Refresh() => RefreshCellPresentation();
        internal void ClearCallbacks() { AlignmentRead = null; WrappingRead = null; TrimmingRead = null; InnerRead = null; }
    }
    private sealed class TestValue : CellValue
    {
        internal string Text = "Owned content";
        internal Func<CellKind>? KindRead;
        public override CellKind ContentKind => KindRead?.Invoke() ?? CellKind.Text;
        public override object? Value => Text;
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
    }
}
