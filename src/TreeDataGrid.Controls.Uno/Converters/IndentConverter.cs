using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Uno.Controls.Converters;

/// <summary>Converts hierarchy depth to the reference 20-pixel indentation.</summary>
public class IndentConverter : IValueConverter
{
    public static IndentConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is int indent ? new Thickness(20 * indent, 0, 0, 0) : new Thickness();
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
