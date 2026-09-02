using System;
using System.Globalization;
namespace TreeDataGridCore
{
    public enum GridUnitType { Auto, Pixel, Star }
    public readonly struct GridLength : IEquatable<GridLength>
    {
        public GridLength(double value, GridUnitType unit = GridUnitType.Pixel)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
            GridUnitType = unit;
        }
        public double Value { get; }
        public GridUnitType GridUnitType { get; }
        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsStar => GridUnitType == GridUnitType.Star;
        public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;
        public static GridLength Auto => new GridLength(1, GridUnitType.Auto);
        public static GridLength Star => new GridLength(1, GridUnitType.Star);
        public bool Equals(GridLength other) => Value.Equals(other.Value) && GridUnitType == other.GridUnitType;
        public override bool Equals(object? other) => other is GridLength length && Equals(length);
        public override int GetHashCode() => HashCode.Combine(Value, GridUnitType);
        public static bool operator ==(GridLength x, GridLength y) => x.Equals(y);
        public static bool operator !=(GridLength x, GridLength y) => !x.Equals(y);
        public override string ToString() => IsAuto ? "Auto" : Value.ToString(CultureInfo.InvariantCulture) + (IsStar ? "*" : "");
        public static GridLength Parse(string text)
        {
            text = text.Trim();
            if (string.Equals(text, "Auto", StringComparison.OrdinalIgnoreCase))
                return Auto;
            var star = text.EndsWith("*", StringComparison.Ordinal);
            var number = star ? text.Substring(0, text.Length - 1) : text;
            return new GridLength(number.Length == 0 && star ? 1 : double.Parse(number, CultureInfo.InvariantCulture), star ? GridUnitType.Star : GridUnitType.Pixel);
        }
    }
}
