using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TreeDataGridCore;
using TreeDataGridDemo.Models;
using Uno.Controls;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample.Demo
{
    /// <summary>Exposes <see cref="Countries.Regions"/>; WinUI XAML has no x:Static.</summary>
    public sealed class CountryRegions
    {
        public IReadOnlyList<string> Items => Countries.Regions;
    }

    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public bool IsInverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language) =>
            (value is true) != IsInverted ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            (value is Visibility.Visible) != IsInverted;
    }

    /// <summary>Selects the folder icon from IsExpanded; the Avalonia demo uses a MultiBinding.</summary>
    public sealed class FolderIconConverter : IValueConverter
    {
        private static readonly Uri s_folder = new("ms-appx:///Assets/folder.png");
        private static readonly Uri s_folderOpen = new("ms-appx:///Assets/folder-open.png");
        private BitmapImage? _folder;
        private BitmapImage? _folderOpen;

        public object Convert(object value, Type targetType, object parameter, string language) =>
            value is true ? _folderOpen ??= new BitmapImage(s_folderOpen) : _folder ??= new BitmapImage(s_folder);

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Native counterpart of the Avalonia demo styles
    /// <c>:is(TreeDataGridCell):nth-last-child(1)</c> and
    /// <c>TreeDataGridColumnHeader:nth-last-child(1)</c>, which make the last column bold.
    /// </summary>
    public static class LastColumnEmphasis
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(LastColumnEmphasis), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(TreeDataGrid grid) => (bool)grid.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(TreeDataGrid grid, bool value) => grid.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeDataGrid)d;
            grid.CellPrepared -= OnCellPrepared;
            grid.CellClearing -= OnCellClearing;
            grid.LayoutUpdated -= OnLayoutUpdated;
            if (e.NewValue is true)
            {
                grid.CellPrepared += OnCellPrepared;
                grid.CellClearing += OnCellClearing;
                grid.LayoutUpdated += OnLayoutUpdated;
            }

            void OnLayoutUpdated(object? sender, object args) => UpdateHeaders(grid);
        }

        private static int LastColumnIndex(TreeDataGrid grid) =>
            (grid.Source?.Columns.Count ?? 0) - 1;

        private static void OnCellPrepared(object? sender, TreeDataGridCellEventArgs e)
        {
            if (sender is TreeDataGrid grid && e.ColumnIndex == LastColumnIndex(grid))
                e.Cell.FontWeight = FontWeights.Bold;
        }

        private static void OnCellClearing(object? sender, TreeDataGridCellEventArgs e)
        {
            e.Cell.ClearValue(Control.FontWeightProperty);
        }

        private static void UpdateHeaders(TreeDataGrid grid)
        {
            var last = LastColumnIndex(grid);
            foreach (var header in FindHeaders(grid))
            {
                if (header.ColumnIndex == last)
                {
                    if (header.FontWeight.Weight != FontWeights.Bold.Weight)
                        header.FontWeight = FontWeights.Bold;
                }
                else if (header.ReadLocalValue(Control.FontWeightProperty) != DependencyProperty.UnsetValue)
                {
                    header.ClearValue(Control.FontWeightProperty);
                }
            }
        }

        private static IEnumerable<TreeDataGridColumnHeader> FindHeaders(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; ++i)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TreeDataGridColumnHeader header)
                    yield return header;
                else if (child is not TreeDataGridRowsPresenter)
                {
                    foreach (var nested in FindHeaders(child))
                        yield return nested;
                }
            }
        }
    }

    /// <summary>
    /// Shows an article thumbnail once its download has decoded. The Avalonia model raises
    /// a property change when its bitmap loads; the Uno model keeps one BitmapImage identity,
    /// so the Image re-subscribes to it after <c>ImageLoadingTask</c> completes.
    /// </summary>
    public static class ArticleImage
    {
        public static readonly DependencyProperty ArticleProperty = DependencyProperty.RegisterAttached(
            "Article", typeof(object), typeof(ArticleImage), new PropertyMetadata(null, OnArticleChanged));

        public static object? GetArticle(Image image) => image.GetValue(ArticleProperty);
        public static void SetArticle(Image image, object? value) => image.SetValue(ArticleProperty, value);

        private static async void OnArticleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = (Image)d;
            if (e.NewValue is not OnThisDayArticle article)
            {
                image.Source = null;
                return;
            }

            var bitmap = article.Image;
            image.Source = bitmap;
            if (bitmap is null || article.ImageLoadingTask.IsCompleted)
                return;
            await article.ImageLoadingTask;
            if (ReferenceEquals(GetArticle(image), article))
            {
                image.Source = null;
                image.Source = bitmap;
            }
        }
    }

    internal static class TreeDataGridSourceExtensions
    {
        public static int FindDisplayedRowIndex(this ITreeDataGridSource source, object? item)
        {
            if (item is null)
            {
                return -1;
            }

            for (var index = 0; index < source.Rows.Count; index++)
            {
                if (ReferenceEquals(source.Rows[index].Model, item))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
