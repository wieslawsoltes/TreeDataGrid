using System;
using Avalonia.Utilities;

namespace Avalonia.Data
{
    public enum BindingMode
    {
        Default,
        OneWay,
        TwoWay,
        OneTime,
        OneWayToSource,
    }

    public enum BindingPriority
    {
        LocalValue = 0,
        Style = 1,
    }

    public readonly struct BindingValue<T>
    {
        private readonly T? _value;

        public BindingValue(T? value)
        {
            _value = value;
            Error = null;
            HasValue = true;
        }

        public bool HasValue { get; }
        public T Value => _value!;
        public Exception? Error { get; }

        private BindingValue(T? value, Exception? error, bool hasValue)
        {
            _value = value;
            Error = error;
            HasValue = hasValue;
        }

        public static implicit operator BindingValue<T>(T? value) => new(value);

        public static BindingValue<T> BindingError(Exception error, Optional<T> fallback)
        {
            return fallback.HasValue ?
                new BindingValue<T>(fallback.Value, error, true) :
                new BindingValue<T>(default, error, false);
        }
    }
}
