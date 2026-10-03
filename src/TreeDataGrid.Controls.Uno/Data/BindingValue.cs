using System;
using System.Collections.Generic;

namespace Uno.Data;

/// <summary>A typed optional value; an explicitly supplied null is distinct from absence.</summary>
public readonly struct Optional<T> : IEquatable<Optional<T>>
{
    private readonly T _value;
    public Optional(T value) { _value = value; HasValue = true; }
    public bool HasValue { get; }
    public T Value => HasValue ? _value : throw new InvalidOperationException("The optional value is unset.");
    public T GetValueOrDefault() => _value;
    public T GetValueOrDefault(T defaultValue) => HasValue ? _value : defaultValue;
    public bool Equals(Optional<T> other) => HasValue == other.HasValue &&
        (!HasValue || EqualityComparer<T>.Default.Equals(_value, other._value));
    public override bool Equals(object? other) => other is Optional<T> value && Equals(value);
    public override int GetHashCode() => HasValue ? HashCode.Combine(true, _value) : 0;
    public static implicit operator Optional<T>(T value) => new(value);
    public static bool operator ==(Optional<T> first, Optional<T> second) => first.Equals(second);
    public static bool operator !=(Optional<T> first, Optional<T> second) => !first.Equals(second);
}

/// <summary>Typed-binding modes, including modes absent from WinUI's native enum.</summary>
public enum BindingMode { Default, OneWay, TwoWay, OneTime, OneWayToSource }

[Flags]
public enum BindingValueType
{
    UnsetValue = 0, DoNothing = 1, TypeMask = 255, HasValue = 256,
    Value = HasValue | 2, HasError = 512, BindingError = HasError | 3,
    DataValidationError = HasError | 4,
    BindingErrorWithFallback = BindingError | HasValue,
    DataValidationErrorWithFallback = DataValidationError | HasValue,
}

/// <summary>Unboxed values and diagnostics for the native typed-binding extension.</summary>
/// <remarks>Exception identity is preserved. This is not a native dependency-property value.</remarks>
public readonly struct BindingValue<T> : IEquatable<BindingValue<T>>
{
    private readonly T _value;
    public BindingValue(T value) : this(BindingValueType.Value, value, null) { }
    private BindingValue(BindingValueType type, T value, Exception? error)
    { Type = type; _value = value; Error = error; }
    public BindingValueType Type { get; }
    public bool HasValue => (Type & BindingValueType.HasValue) != 0;
    public bool HasError => (Type & BindingValueType.HasError) != 0;
    public Exception? Error { get; }
    public T Value => HasValue ? _value : throw new InvalidOperationException("The binding value is unset.");
    public static BindingValue<T> Unset => default;
    public static BindingValue<T> DoNothing => new(BindingValueType.DoNothing, default!, null);
    public static BindingValue<T> BindingError(Exception error) => BindingError(error, default(Optional<T>));
    public static BindingValue<T> BindingError(Exception error, T fallback) => BindingError(error, new Optional<T>(fallback));
    public static BindingValue<T> BindingError(Exception error, Optional<T> fallback)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(fallback.HasValue ? BindingValueType.BindingErrorWithFallback : BindingValueType.BindingError,
            fallback.GetValueOrDefault(), error);
    }
    public static BindingValue<T> DataValidationError(Exception error) => DataValidationError(error, default(Optional<T>));
    public static BindingValue<T> DataValidationError(Exception error, T fallback) => DataValidationError(error, new Optional<T>(fallback));
    public static BindingValue<T> DataValidationError(Exception error, Optional<T> fallback)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(fallback.HasValue ? BindingValueType.DataValidationErrorWithFallback : BindingValueType.DataValidationError,
            fallback.GetValueOrDefault(), error);
    }
    public T GetValueOrDefault() => _value;
    public T GetValueOrDefault(T defaultValue) => HasValue ? _value : defaultValue;
    public Optional<T> ToOptional() => HasValue ? new(_value) : default;
    public BindingValue<T> WithValue(T value) => HasError
        ? new(Type | BindingValueType.HasValue, value, Error) : new(value);
    public bool Equals(BindingValue<T> other) => Type == other.Type && ReferenceEquals(Error, other.Error) &&
        (!HasValue || EqualityComparer<T>.Default.Equals(_value, other._value));
    public override bool Equals(object? other) => other is BindingValue<T> value && Equals(value);
    public override int GetHashCode() => HashCode.Combine(Type, HasValue ? _value : default,
        Error is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Error));
    public static implicit operator BindingValue<T>(T value) => new(value);
    public static implicit operator BindingValue<T>(Optional<T> value) => value.HasValue ? new(value.Value) : Unset;
    public static bool operator ==(BindingValue<T> first, BindingValue<T> second) => first.Equals(second);
    public static bool operator !=(BindingValue<T> first, BindingValue<T> second) => !first.Equals(second);
}
