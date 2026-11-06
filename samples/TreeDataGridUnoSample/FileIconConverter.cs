using System;
using Microsoft.UI.Xaml.Data;

namespace TreeDataGridUnoSample;

public sealed class FileIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "ms-appx:///Assets/folder.png" : "ms-appx:///Assets/file.png";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
