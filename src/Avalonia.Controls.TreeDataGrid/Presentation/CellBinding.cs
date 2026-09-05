using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Experimental.Data;
using Core = global::TreeDataGridCore;
namespace Avalonia.Controls.Presentation
{
    internal static class CellBinding
    {
        public static TypedBinding<TModel, TValue> Create<TModel, TValue>(Core.Models.ValueColumn<TModel, TValue> column) where TModel : class
        {
            var compiled = column.GetterExpression is { } expression ?
                ExpressionCache<TModel, TValue>.Bindings.GetValue(expression, static x => TypedBinding<TModel>.OneWay(x)) : null;
            // Cache compiled delegates, never per-view mutable binding settings or row subscriptions.
            return new TypedBinding<TModel, TValue>
            {
                Read = compiled?.Read ?? column.Getter,
                Write = column.Setter,
                Links = compiled?.Links is { } links ? (Func<TModel, object>[])links.Clone() : [static model => model],
                Mode = column.Setter is null ? BindingMode.OneWay : BindingMode.TwoWay,
            };
        }
        private static class ExpressionCache<TModel, TValue> where TModel : class
        {
            public static readonly ConditionalWeakTable<Expression<Func<TModel, TValue>>, TypedBinding<TModel, TValue>> Bindings = new();
        }
    }
}
