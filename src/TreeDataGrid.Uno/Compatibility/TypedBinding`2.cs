using System;
using Avalonia.Data;
using Avalonia.Experimental.Data.Core;
using Avalonia.Utilities;

namespace Avalonia.Experimental.Data
{
    public class TypedBinding<TIn, TOut>
        where TIn : class
    {
        public Func<TIn, TOut>? Read { get; set; }
        public Action<TIn, TOut>? Write { get; set; }
        public Func<TIn, object>[]? Links { get; set; }
        public BindingMode Mode { get; set; }
        public BindingPriority Priority { get; set; }
        public Optional<TOut> FallbackValue { get; set; }
        public Optional<TIn> Source { get; set; }

        public TypedBindingExpression<TIn, TOut> Instance(TIn? source, BindingMode mode = BindingMode.OneWay)
        {
            _ = Read ?? throw new InvalidOperationException("Cannot create a typed binding expression without a read delegate.");
            _ = Links ?? throw new InvalidOperationException("Cannot create a typed binding expression without expression links.");

            if ((mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource) && Write is null)
                throw new InvalidOperationException("Cannot create a two-way typed binding expression without a write delegate.");

            return new TypedBindingExpression<TIn, TOut>(source, Read, Write, Links, FallbackValue);
        }
    }
}
