using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Uno.Data;
using Uno.Data.Core.Parsers;

namespace Uno.Experimental.Data;

/// <summary>Typed factories for handwritten expressions and generated delegate/link descriptors.</summary>
public static class TypedBinding<TIn> where TIn : class
{
    public static TypedBinding<TIn, TOut> OneWay<TOut>(Func<TIn, TOut> read, Func<TIn, object>[] links) =>
        new() { Read = read ?? throw new ArgumentNullException(nameof(read)),
            Links = links ?? throw new ArgumentNullException(nameof(links)), Mode = BindingMode.OneWay };

    public static TypedBinding<TIn, TOut> Default<TOut>(Expression<Func<TIn, TOut>> read, Action<TIn, TOut> write) =>
        Create(read, write ?? throw new ArgumentNullException(nameof(write)), BindingMode.Default);
    public static TypedBinding<TIn, TOut> OneWay<TOut>(Expression<Func<TIn, TOut>> read) =>
        Create(read, null, BindingMode.OneWay);
    public static TypedBinding<TIn, TOut> TwoWay<TOut>(Expression<Func<TIn, TOut>> read, Action<TIn, TOut> write) =>
        Create(read, write ?? throw new ArgumentNullException(nameof(write)), BindingMode.TwoWay);
    public static TypedBinding<TIn, TOut> OneTime<TOut>(Expression<Func<TIn, TOut>> read) =>
        Create(read, null, BindingMode.OneTime);

    public static TypedBinding<TIn, TOut> TwoWay<TOut>(Expression<Func<TIn, TOut>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression member = expression.Body;
        while (member is UnaryExpression { NodeType: ExpressionType.Convert } convert) member = convert.Operand;
        if (member is not MemberExpression { Member: PropertyInfo property } propertyAccess ||
            property.GetGetMethod(true) is not { IsPrivate: false } ||
            property.GetSetMethod(true) is not { IsPrivate: false })
            throw new ArgumentException("A two-way expression must target a property with accessible getter and setter.", nameof(expression));
        var value = Expression.Parameter(typeof(TOut), "value");
        var assign = Expression.Assign(propertyAccess, Expression.Convert(value, propertyAccess.Type));
        var setter = Expression.Lambda<Action<TIn, TOut>>(assign, expression.Parameters[0], value)
            .Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled);
        return Create(expression, setter, BindingMode.TwoWay);
    }
    private static TypedBinding<TIn, TOut> Create<TOut>(Expression<Func<TIn, TOut>> read,
        Action<TIn, TOut>? write, BindingMode mode)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new()
        {
            Read = read.Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled),
            Write = write,
            Links = ExpressionChainVisitor<TIn>.Build(read),
            Mode = mode,
        };
    }
}
