using System;
using System.ComponentModel;
using System.Globalization;
using Windows.Foundation.Metadata;

namespace Avalonia
{
    public enum GridUnitType
    {
        Auto,
        Pixel,
        Star,
    }

    [CreateFromString(MethodName = nameof(Parse))]
    [TypeConverter(typeof(GridLengthTypeConverter))]
    public readonly struct GridLength : IEquatable<GridLength>
    {
        public GridLength(double value, GridUnitType gridUnitType = GridUnitType.Pixel)
        {
            Value = value;
            GridUnitType = gridUnitType;
        }

        public double Value { get; }
        public GridUnitType GridUnitType { get; }
        public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;
        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsStar => GridUnitType == GridUnitType.Star;

        public static GridLength Auto { get; } = new(1, GridUnitType.Auto);

        public bool Equals(GridLength other) => Value.Equals(other.Value) && GridUnitType == other.GridUnitType;
        public override bool Equals(object? obj) => obj is GridLength other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value, (int)GridUnitType);
        public static bool operator ==(GridLength left, GridLength right) => left.Equals(right);
        public static bool operator !=(GridLength left, GridLength right) => !left.Equals(right);

        public static GridLength Parse(string text)
        {
            return GridLengthTypeConverter.Parse(text, CultureInfo.InvariantCulture);
        }
    }

    public sealed class GridLengthTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string text)
                return Parse(text, culture ?? CultureInfo.InvariantCulture);

            return base.ConvertFrom(context, culture, value);
        }

        internal static GridLength Parse(string value, CultureInfo culture)
        {
            var text = value.Trim();

            if (text.Length == 0)
                throw new FormatException("GridLength cannot be parsed from an empty string.");

            if (string.Equals(text, "Auto", StringComparison.OrdinalIgnoreCase))
                return GridLength.Auto;

            if (text.EndsWith('*'))
            {
                var starText = text[..^1].Trim();

                if (starText.Length == 0)
                    return new GridLength(1, GridUnitType.Star);

                if (double.TryParse(starText, NumberStyles.Float, culture, out var stars))
                    return new GridLength(stars, GridUnitType.Star);

                throw new FormatException($"GridLength star value '{value}' is invalid.");
            }

            if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                text = text[..^2].TrimEnd();

            if (double.TryParse(text, NumberStyles.Float, culture, out var pixels))
                return new GridLength(pixels, GridUnitType.Pixel);

            throw new FormatException($"GridLength value '{value}' is invalid.");
        }
    }

    public readonly struct Size
    {
        public Size(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }
    }

    public readonly struct Point : IEquatable<Point>
    {
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Point other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Point left, Point right) => left.Equals(right);
        public static bool operator !=(Point left, Point right) => !left.Equals(right);
    }

    public readonly struct Rect
    {
        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rect(Size size)
            : this(0, 0, size.Width, size.Height)
        {
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
    }
}
