using Avalonia.Data;

namespace Avalonia.Controls
{
    internal class TreeDataGridBindingProbe : StyledElement
    {
        public static readonly StyledProperty<object?> ValueProperty =
            AvaloniaProperty.Register<TreeDataGridBindingProbe, object?>(
                nameof(Value),
                defaultBindingMode: BindingMode.TwoWay);

        public object? Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }
}
