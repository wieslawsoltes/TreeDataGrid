using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Compare guarded text publication with the original native setter.</summary>
internal static class NativeTextAssignmentRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var reference = new TextBlock();
        var candidate = new ProbeTextCell { Width = 260, Height = 40 };
        var host = new StackPanel();
        host.Children.Add(reference);
        host.Children.Add(candidate);
        var source = new NativeTextAssignmentSource();
        string? current = null;
        candidate.Read = () => current;
        long callback = 0;
        TextBlock? native = null;
        var cases = 0;
        try
        {
            page.Content = host;
            candidate.ApplyTemplate();
            await Task.Delay(80);
            host.UpdateLayout();
            native = ShowcaseRuntimeChecks.Descendants(candidate).OfType<TextBlock>().Single(text => text.Name == "PART_Text");
            // Exercise publication to the text block itself: text selection makes it ineligible
            // for direct Skia rendering, which assigns the block's text only while it displays it.
            native.IsTextSelectionEnabled = true;

            reference.ClearValue(TextBlock.TextProperty);
            native.ClearValue(TextBlock.TextProperty);
            Publish(null);
            Check(native.ReadLocalValue(TextBlock.TextProperty) is string,
                "An equal default text value did not acquire the original local priority.");
            ++cases;

            var first = new string("Native text publication".ToCharArray());
            Publish(first);
            var reads = candidate.Reads;
            for (var iteration = 0; iteration < 4096; ++iteration) candidate.Refresh();
            Check(candidate.Reads == reads + 4096,
                "Warm text publication stopped evaluating the application display getter.");
            SameState();
            ++cases;

            var equalButDistinct = new string(first.ToCharArray());
            Check(!ReferenceEquals(first, equalButDistinct), "The string identity fixture was interned.");
            Publish(equalButDistinct);
            ++cases;

            foreach (var mode in new[] { BindingMode.OneWay, BindingMode.OneTime })
            {
                source.Text = first;
                reference.SetBinding(TextBlock.TextProperty, new Binding { Source = source, Path = new PropertyPath(nameof(NativeTextAssignmentSource.Text)), Mode = mode });
                native.SetBinding(TextBlock.TextProperty, new Binding { Source = source, Path = new PropertyPath(nameof(NativeTextAssignmentSource.Text)), Mode = mode });
                host.UpdateLayout();
                Check(reference.Text == native.Text && reference.Text == first,
                    $"The native-binding control did not provide the same starting value: mode={mode}, " +
                    $"reference='{reference.Text}', native='{native.Text}', expected='{first}'.");
                // An equal effective value from a binding is not proof that the
                // cell owns that local slot. Compare the original setter itself.
                Publish(first);
                source.Text = "Changed binding source";
                host.UpdateLayout();
                SameState();
                reference.ClearValue(TextBlock.TextProperty);
                native.ClearValue(TextBlock.TextProperty);
                Publish(first);
                ++cases;
            }

            var failure = new InvalidOperationException("Application display getter failed.");
            candidate.Read = () => throw failure;
            Exception? observed = null;
            try { candidate.Refresh(); }
            catch (Exception error) { observed = error; }
            Check(ReferenceEquals(observed, failure) && native.Text == first,
                "A throwing display getter changed the last published text or lost exception identity.");
            ++cases;

            const string newer = "Newer reentrant display";
            candidate.Read = () =>
            {
                candidate.Read = () => newer;
                candidate.Refresh();
                return "Obsolete outer display";
            };
            candidate.Refresh();
            reference.Text = newer;
            SameState();
            ++cases;

            const string trigger = "Trigger native callback";
            const string replacement = "Native callback replacement";
            var invoked = false;
            callback = native.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) =>
            {
                if (invoked || native.Text != trigger) return;
                invoked = true;
                candidate.Read = () => replacement;
                candidate.Refresh();
            });
            candidate.Read = () => trigger;
            candidate.Refresh();
            reference.Text = replacement;
            Check(invoked, "The native text callback did not execute.");
            SameState();
            native.UnregisterPropertyChangedCallback(TextBlock.TextProperty, callback);
            callback = 0;
            ++cases;

            candidate.Read = () => current;
            Publish(string.Empty);
            Publish(null);
            ++cases;
            Console.WriteLine($"UNO_RUNTIME_NATIVE_TEXT_ASSIGNMENT_PASSED: cases={cases}; local priority, string identity, 4096 live getter reads, OneWay/OneTime binding ownership, original exceptions, nested render precedence, native callbacks and null text");

            void Publish(string? value)
            {
                current = value;
                reference.Text = value ?? string.Empty;
                candidate.Refresh();
                SameState();
            }
            void SameState()
            {
                Check(string.Equals(reference.Text, native.Text, StringComparison.Ordinal),
                    "Guarded publication differs from the original native text setter.");
                Check(ReferenceEquals(reference.Text, native.Text),
                    "Guarded publication changed the native string-instance ownership.");
                var expectedLocal = reference.ReadLocalValue(TextBlock.TextProperty);
                var actualLocal = native.ReadLocalValue(TextBlock.TextProperty);
                Check(expectedLocal is string expected ? actualLocal is string actual && ReferenceEquals(expected, actual) :
                    ReferenceEquals(expectedLocal, DependencyProperty.UnsetValue) == ReferenceEquals(actualLocal, DependencyProperty.UnsetValue),
                    "Guarded publication changed dependency-property local ownership.");
                Check((reference.GetBindingExpression(TextBlock.TextProperty) is null) ==
                    (native.GetBindingExpression(TextBlock.TextProperty) is null),
                    "Guarded publication changed native binding replacement behavior.");
            }
        }
        finally
        {
            candidate.Read = null;
            if (native is not null)
            {
                if (callback != 0) native.UnregisterPropertyChangedCallback(TextBlock.TextProperty, callback);
                native.ClearValue(TextBlock.TextProperty);
            }
            reference.ClearValue(TextBlock.TextProperty);
            page.Content = previous;
        }
        Check(source.Subscribers == 0, "The native text ownership fixture retained source subscriptions.");
    }

    private sealed partial class ProbeTextCell : TreeDataGridTextCell
    {
        internal Func<string?>? Read;
        internal int Reads;
        protected override string? DisplayText { get { ++Reads; return Read?.Invoke(); } }
        internal void Refresh() => RefreshCellPresentation();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

/// <summary>
/// A binding source for the native text assignment check. Uno generates binding accessors
/// only for public bindable types; trimmed browser builds do not preserve reflection.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed partial class NativeTextAssignmentSource : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private PropertyChangedEventHandler? _changed;
    internal int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public string Text
    {
        get => _text;
        set { _text = value; _changed?.Invoke(this, new(nameof(Text))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
