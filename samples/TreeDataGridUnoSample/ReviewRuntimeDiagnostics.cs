using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Bounded native-layout failure history; never changes a regression's assertions or outcome.</summary>
internal static class ReviewRuntimeDiagnostics
{
    internal static async Task RunGenericPresenterAsync(MainPage page)
    {
        ScrollViewer? scroll = null;
        var history = new List<string>(64);
        var token = page.RegisterPropertyChangedCallback(MainPage.ContentProperty, (_, _) => Attach());
        try
        {
            Attach();
            await GenericPresenterRuntimeChecks.RunAsync(page);
        }
        catch
        {
            foreach (var state in history.TakeLast(20)) Console.WriteLine("UNO_PRESENTER_STATE: " + state);
            throw;
        }
        finally
        {
            page.UnregisterPropertyChangedCallback(MainPage.ContentProperty, token);
            Detach();
        }

        void Attach()
        {
            if (ReferenceEquals(scroll, page.Content)) return;
            Detach();
            scroll = page.Content as ScrollViewer;
            if (scroll is null) return;
            scroll.LayoutUpdated += OnLayout;
            scroll.ViewChanged += OnView;
        }
        void Detach()
        {
            if (scroll is null) return;
            scroll.LayoutUpdated -= OnLayout;
            scroll.ViewChanged -= OnView;
            scroll = null;
        }
        void OnLayout(object? sender, object args) => Capture();
        void OnView(object? sender, ScrollViewerViewChangedEventArgs args) => Capture();
        void Capture()
        {
            if (scroll?.Content is not TreeDataGridRowsPresenter rows || rows.Items is not { Count: > 160 }) return;
            var first = int.MaxValue;
            var last = -1;
            foreach (var row in rows.RealizedRows)
            {
                first = Math.Min(first, row.RowIndex);
                last = Math.Max(last, row.RowIndex);
            }
            var target = rows.TryGetElement(160);
            var state = string.Create(CultureInfo.InvariantCulture,
                $"offset={scroll.HorizontalOffset:F2},{scroll.VerticalOffset:F2}; viewport={scroll.ViewportWidth:F2},{scroll.ViewportHeight:F2}; extent={scroll.ExtentWidth:F2},{scroll.ExtentHeight:F2}; target={rows.GetRowStart(160):F2}+{rows.GetRowHeight(160):F2}; realized={first}..{last}/{rows.RealizedRows.Count}; targetIndex={target?.RowIndex}; targetModel={target?.Model}");
            if (history.Count > 0 && history[^1] == state) return;
            if (history.Count == 64) history.RemoveAt(0);
            history.Add(state);
        }
    }
}
