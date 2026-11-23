using System;
using Microsoft.UI.Xaml;
using Uno.Controls.Converters;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class IndentConverterTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 20)]
    [InlineData(3, 60)]
    public void Depth_uses_reference_spacing(int depth, double expected)
    {
        var value = Assert.IsType<Thickness>(IndentConverter.Instance.Convert(depth, typeof(Thickness), null!, "en-US"));
        Assert.Equal(expected, value.Left);
        Assert.Equal(0, value.Top);
        Assert.Equal(0, value.Right);
        Assert.Equal(0, value.Bottom);
    }
    [Fact]
    public void Non_integer_input_is_zero_and_reverse_conversion_is_unsupported()
    {
        Assert.Equal(new Thickness(), IndentConverter.Instance.Convert("depth", typeof(Thickness), null!, "en-US"));
        Assert.Throws<NotImplementedException>(() => IndentConverter.Instance.ConvertBack(new Thickness(20), typeof(int), null!, "en-US"));
    }
}
