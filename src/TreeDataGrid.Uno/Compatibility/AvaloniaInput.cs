using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Avalonia.Input
{
    [Flags]
    public enum DragDropEffects
    {
        None = 0,
        Copy = 1,
        Move = 2,
        Link = 4,
    }

    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4,
        Meta = 8,
    }

    public enum Key
    {
        None = (int)VirtualKey.None,
        Enter = (int)VirtualKey.Enter,
        Escape = (int)VirtualKey.Escape,
        Left = (int)VirtualKey.Left,
        Up = (int)VirtualKey.Up,
        Right = (int)VirtualKey.Right,
        Down = (int)VirtualKey.Down,
        F2 = (int)VirtualKey.F2,
        Home = (int)VirtualKey.Home,
        End = (int)VirtualKey.End,
        PageUp = (int)VirtualKey.PageUp,
        PageDown = (int)VirtualKey.PageDown,
    }

    public enum NavigationDirection
    {
        Up,
        Down,
        Left,
        Right,
        First,
        Last,
    }

    public enum PointerType
    {
        Mouse,
        Pen,
        Touch,
        Unknown,
    }

    public enum PointerUpdateKind
    {
        Other,
        LeftButtonPressed,
        LeftButtonReleased,
        RightButtonPressed,
        RightButtonReleased,
        MiddleButtonPressed,
        MiddleButtonReleased,
    }

    public sealed class DataFormat<T>
    {
        internal DataFormat(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public static class DataFormat
    {
        public static DataFormat<string> CreateStringApplicationFormat(string name)
        {
            return new DataFormat<string>(name);
        }
    }

    public readonly struct PointerPointProperties
    {
        public PointerPointProperties(bool isRightButtonPressed, PointerUpdateKind pointerUpdateKind)
        {
            IsRightButtonPressed = isRightButtonPressed;
            PointerUpdateKind = pointerUpdateKind;
        }

        public bool IsRightButtonPressed { get; }
        public PointerUpdateKind PointerUpdateKind { get; }
    }

    public readonly struct PointerPoint
    {
        public PointerPoint(global::Avalonia.Point position, PointerPointProperties properties)
        {
            Position = position;
            Properties = properties;
        }

        public global::Avalonia.Point Position { get; }
        public PointerPointProperties Properties { get; }
    }

    public sealed class Pointer
    {
        public Pointer(PointerType type)
        {
            Type = type;
        }

        public PointerType Type { get; }
    }

    public abstract class RoutedInputEventArgs : EventArgs
    {
        public abstract bool Handled { get; set; }
        public KeyModifiers KeyModifiers { get; protected init; }
        public object? Source { get; protected init; }
    }

    public sealed class KeyEventArgs : RoutedInputEventArgs
    {
        private readonly KeyRoutedEventArgs _inner;

        public KeyEventArgs(KeyRoutedEventArgs inner)
        {
            _inner = inner;
            Key = (Key)inner.Key;
            KeyModifiers = InputCompatibility.GetKeyModifiers();
            Source = inner.OriginalSource;
        }

        public Key Key { get; }

        public override bool Handled
        {
            get => _inner.Handled;
            set => _inner.Handled = value;
        }
    }

    public sealed class TextInputEventArgs : RoutedInputEventArgs
    {
        public TextInputEventArgs(string? text)
        {
            Text = text;
            KeyModifiers = InputCompatibility.GetKeyModifiers();
        }

        public string? Text { get; }
        public override bool Handled { get; set; }
    }

    public class PointerEventArgs : RoutedInputEventArgs
    {
        private readonly PointerRoutedEventArgs _inner;

        public PointerEventArgs(PointerRoutedEventArgs inner)
        {
            _inner = inner;
            Pointer = new Pointer(InputCompatibility.MapPointerType(inner.Pointer.PointerDeviceType));
            KeyModifiers = InputCompatibility.GetKeyModifiers();
            Source = inner.OriginalSource;
        }

        public Pointer Pointer { get; }

        public override bool Handled
        {
            get => _inner.Handled;
            set => _inner.Handled = value;
        }

        public PointerPoint GetCurrentPoint(object? relativeTo)
        {
            var point = _inner.GetCurrentPoint(relativeTo as UIElement);
            return new PointerPoint(
                new global::Avalonia.Point(point.Position.X, point.Position.Y),
                new PointerPointProperties(
                    point.Properties.IsRightButtonPressed,
                    InputCompatibility.MapPointerUpdateKind(point.Properties.PointerUpdateKind)));
        }

        public global::Avalonia.Point GetPosition(object? relativeTo)
        {
            var point = _inner.GetCurrentPoint(relativeTo as UIElement);
            return new global::Avalonia.Point(point.Position.X, point.Position.Y);
        }
    }

    public sealed class PointerPressedEventArgs : PointerEventArgs
    {
        public PointerPressedEventArgs(PointerRoutedEventArgs inner)
            : base(inner)
        {
        }
    }

    public sealed class PointerReleasedEventArgs : PointerEventArgs
    {
        public PointerReleasedEventArgs(PointerRoutedEventArgs inner)
            : base(inner)
        {
        }
    }

    public static class KeyExtensions
    {
        public static NavigationDirection? ToNavigationDirection(this Key key)
        {
            return key switch
            {
                Key.Up => NavigationDirection.Up,
                Key.Down => NavigationDirection.Down,
                Key.Left => NavigationDirection.Left,
                Key.Right => NavigationDirection.Right,
                Key.Home => NavigationDirection.First,
                Key.End => NavigationDirection.Last,
                _ => null,
            };
        }
    }

    internal static class InputCompatibility
    {
        public static KeyEventArgs ToAvalonia(this KeyRoutedEventArgs e) => new(e);
        public static PointerPressedEventArgs ToAvaloniaPressed(this PointerRoutedEventArgs e) => new(e);
        public static PointerReleasedEventArgs ToAvaloniaReleased(this PointerRoutedEventArgs e) => new(e);
        public static PointerEventArgs ToAvalonia(this PointerRoutedEventArgs e) => new(e);
        public static TextInputEventArgs ToAvaloniaText(this string? text) => new(text);

        internal static KeyModifiers GetKeyModifiers()
        {
            var result = KeyModifiers.None;

            if (IsModifierKeyDown(VirtualKey.Shift))
                result |= KeyModifiers.Shift;
            if (IsModifierKeyDown(VirtualKey.Control))
                result |= KeyModifiers.Control;
            if (IsModifierKeyDown(VirtualKey.Menu))
                result |= KeyModifiers.Alt;
            if (IsModifierKeyDown(VirtualKey.LeftWindows) || IsModifierKeyDown(VirtualKey.RightWindows))
                result |= KeyModifiers.Meta;

            return result;
        }

        private static bool IsModifierKeyDown(VirtualKey key)
        {
            return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        internal static PointerType MapPointerType(Microsoft.UI.Input.PointerDeviceType type)
        {
            return type switch
            {
                Microsoft.UI.Input.PointerDeviceType.Mouse => PointerType.Mouse,
                Microsoft.UI.Input.PointerDeviceType.Pen => PointerType.Pen,
                Microsoft.UI.Input.PointerDeviceType.Touch => PointerType.Touch,
                _ => PointerType.Unknown,
            };
        }

        internal static PointerUpdateKind MapPointerUpdateKind(Microsoft.UI.Input.PointerUpdateKind kind)
        {
            return kind switch
            {
                Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed => PointerUpdateKind.LeftButtonPressed,
                Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased => PointerUpdateKind.LeftButtonReleased,
                Microsoft.UI.Input.PointerUpdateKind.RightButtonPressed => PointerUpdateKind.RightButtonPressed,
                Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased => PointerUpdateKind.RightButtonReleased,
                Microsoft.UI.Input.PointerUpdateKind.MiddleButtonPressed => PointerUpdateKind.MiddleButtonPressed,
                Microsoft.UI.Input.PointerUpdateKind.MiddleButtonReleased => PointerUpdateKind.MiddleButtonReleased,
                _ => PointerUpdateKind.Other,
            };
        }
    }
}
