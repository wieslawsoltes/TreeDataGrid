using System;

namespace Avalonia
{
    public enum GridUnitType
    {
        Auto,
        Pixel,
        Star,
    }

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
