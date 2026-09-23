using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

internal static class CachedStateRuntimeChecks
{
    public static void Run()
    {
        Verify(TreeDataGridCell.IsSelectedProperty, static (cell, value) => cell.IsSelected = value);
        Verify(TreeDataGridCell.IsCurrentProperty, static (cell, value) => cell.IsCurrent = value);
        Verify(TreeDataGridCell.IsEditingProperty, static (cell, value) => cell.SetEditing(value));
        Verify(TreeDataGridCell.HasValidationErrorProperty, static (cell, value) => cell.SetError(value));
        Console.WriteLine("UNO_RUNTIME_CACHED_STATE_PASSED: Boolean dependency values, notification ordering, equal assignments and native binding/local-value precedence");
    }

    private static void Verify(DependencyProperty property, Action<TestCell, bool> write)
    {
        var source = new ToggleSwitch { IsOn = false };
        var cached = new TestCell();
        var reference = new TestCell();
        var cachedEvents = string.Empty;
        var referenceEvents = string.Empty;
        var cachedToken = cached.RegisterPropertyChangedCallback(property, (_, _) => cachedEvents += cached.GetValue(property));
        var referenceToken = reference.RegisterPropertyChangedCallback(property, (_, _) => referenceEvents += reference.GetValue(property));
        try
        {
            cached.SetBinding(property, new Binding { Source = source, Path = new PropertyPath(nameof(ToggleSwitch.IsOn)), Mode = BindingMode.OneWay });
            reference.SetBinding(property, new Binding { Source = source, Path = new PropertyPath(nameof(ToggleSwitch.IsOn)), Mode = BindingMode.OneWay });
            source.IsOn = true;
            Check(Equals(cached.GetValue(property), true) && Equals(reference.GetValue(property), true), "Native state binding did not initialize.");
            // The equal write must still have exactly native SetValue semantics;
            // an equality shortcut would retain a binding that SetValue replaces.
            write(cached, true);
            reference.SetValue(property, (object)true);
            source.IsOn = false;
            Check(Equals(cached.GetValue(property), reference.GetValue(property)), "Box caching changed binding precedence.");
            foreach (var value in new[] { false, true, true, false })
            {
                write(cached, value);
                reference.SetValue(property, (object)value);
                Check(Equals(cached.GetValue(property), reference.GetValue(property)), "Box caching changed a state value.");
            }
            cached.ClearValue(property);
            reference.ClearValue(property);
            Check(Equals(cached.GetValue(property), reference.GetValue(property)), "Box caching changed ClearValue semantics.");
            Check(cachedEvents == referenceEvents, "Box caching changed native state notification order.");
        }
        finally
        {
            cached.UnregisterPropertyChangedCallback(property, cachedToken);
            reference.UnregisterPropertyChangedCallback(property, referenceToken);
            cached.ClearValue(property);
            reference.ClearValue(property);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class TestCell : TreeDataGridCell
    {
        public void SetEditing(bool value) => IsEditing = value;
        public void SetError(bool value) => HasValidationError = value;
    }
}
