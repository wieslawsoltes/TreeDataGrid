using Microsoft.UI.Xaml;
#if WINDOWS
using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
#endif

namespace Uno.Controls.Primitives;

internal static class ElementParent
{
#if WINDOWS
    // WinUI reports neither a logical nor a visual parent for an element whose tree is not
    // connected to a window (a panel that was never loaded, or a replaced template), although
    // the panel still owns it and rejects adding it elsewhere. The container an element was
    // handed out for is remembered here and confirmed before it is reported.
    private static readonly ConditionalWeakTable<FrameworkElement, WeakReference<FrameworkElement>> s_hosts = new();
#endif

    /// <summary>Remembers the container an element is about to be placed in.</summary>
    internal static void RecordHost(FrameworkElement element, FrameworkElement? host)
    {
#if WINDOWS
        if (host is null) s_hosts.Remove(element);
        else if (s_hosts.TryGetValue(element, out var reference)) reference.SetTarget(host);
        else s_hosts.Add(element, new(host));
#endif
    }

    /// <summary>
    /// The container that currently holds an element: its parent, or on WinUI, when the tree
    /// is not connected to a window, the recorded container that still contains it.
    /// </summary>
    internal static DependencyObject? HostParent(this FrameworkElement element)
    {
#if WINDOWS
        if (element.Parent is { } parent) return parent;
        if (VisualTreeHelper.GetParent(element) is { } visual) return visual;
        if (s_hosts.TryGetValue(element, out var reference) && reference.TryGetTarget(out var host) &&
            (host is Panel panel ? panel.Children.Contains(element) : host is Border border && ReferenceEquals(border.Child, element)))
            return host;
        return null;
#else
        return element.Parent;
#endif
    }
}
