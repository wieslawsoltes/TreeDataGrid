using System;
using System.Linq.Expressions;
using System.Reflection;
using Avalonia.Data;
using Avalonia.Data.Core.Parsers;

#nullable enable

namespace Avalonia.Experimental.Data
{
    /// <summary>
    /// Provides factory methods for creating <see cref="TypedBinding{TIn, TOut}"/> objects from
    /// C# lambda expressions.
    /// </summary>
    /// <typeparam name="TIn">The input type of the binding.</typeparam>
    public static class TypedBinding<TIn>
        where TIn : class
    {
        public static TypedBinding<TIn, TOut> Default<TOut>(
            Expression<Func<TIn, TOut>> read,
            Action<TIn, TOut> write)
        {
            return new TypedBinding<TIn, TOut>
            {
                Read = read.Compile(),
                Write = write,
                Links = ExpressionChainVisitor<TIn>.Build(read),
                Mode = BindingMode.Default,
            };
        }

        public static TypedBinding<TIn, TOut> OneWay<TOut>(Expression<Func<TIn, TOut>> read)
        {
            return new TypedBinding<TIn, TOut>
            {
                Read = read.Compile(),
                Links = ExpressionChainVisitor<TIn>.Build(read),
            };
        }

        public static TypedBinding<TIn, TOut> TwoWay<TOut>(Expression<Func<TIn, TOut>> expression)
        {
            var member = GetMemberExpression(expression.Body);
            var property = member?.Member as System.Reflection.PropertyInfo ??
                throw new ArgumentException(
                    $"Cannot create a two-way binding for '{expression}' because the expression does not target a property.",
                    nameof(expression));

            MethodInfo? getMethodInfo = property.GetGetMethod(true);
            if (getMethodInfo is null || getMethodInfo.IsPrivate)
                throw new ArgumentException(
                    $"Cannot create a two-way binding for '{expression}' because the property has no getter or the getter is private.",
                    nameof(expression));

            MethodInfo? setMethodInfo = property.GetSetMethod(true);
            if (setMethodInfo is null || setMethodInfo.IsPrivate)
                throw new ArgumentException(
                    $"Cannot create a two-way binding for '{expression}' because the property has no setter or the setter is private.",
                    nameof(expression));

            var links = ExpressionChainVisitor<TIn>.Build(expression);
            var value = Expression.Parameter(typeof(TOut), "value");
            var assign = Expression.Assign(member!, Expression.Convert(value, member!.Type));
            var write = Expression.Lambda<Action<TIn, TOut>>(assign, expression.Parameters[0], value).Compile();

            return new TypedBinding<TIn, TOut>
            {
                Read = expression.Compile(),
                Write = write,
                Links = links,
                Mode = BindingMode.TwoWay,
            };
        }

        public static TypedBinding<TIn, TOut> TwoWay<TOut>(
            Expression<Func<TIn, TOut>> read,
            Action<TIn, TOut> write)
        {
            return new TypedBinding<TIn, TOut>
            {
                Read = read.Compile(),
                Write = write,
                Links = ExpressionChainVisitor<TIn>.Build(read),
                Mode = BindingMode.TwoWay,
            };
        }

        public static TypedBinding<TIn, TOut> OneTime<TOut>(Expression<Func<TIn, TOut>> read)
        {
            return new TypedBinding<TIn, TOut>
            {
                Read = read.Compile(),
                Links = ExpressionChainVisitor<TIn>.Build(read),
                Mode = BindingMode.OneTime,
            };
        }

        private static MemberExpression? GetMemberExpression(Expression expression)
        {
            if (expression is MemberExpression member)
                return member;

            if (expression is UnaryExpression unary &&
                unary.NodeType == ExpressionType.Convert)
            {
                return GetMemberExpression(unary.Operand);
            }

            return null;
        }
    }
}
