using System;
using System.Linq.Expressions;
using System.Reflection;

#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    internal static class TreeDataGridExpressionHelper
    {
        public static string? TryGetMemberName<TModel, TValue>(Expression<Func<TModel, TValue>> expression)
        {
            return GetMemberExpression(expression.Body)?.Member.Name;
        }

        public static Action<TModel, TValue?>? TryCreateSetter<TModel, TValue>(Expression<Func<TModel, TValue?>> expression)
        {
            var member = GetMemberExpression(expression.Body);

            if (member?.Member is PropertyInfo property && property.CanWrite)
            {
                var model = expression.Parameters[0];
                var value = Expression.Parameter(typeof(TValue), "value");
                var assign = Expression.Assign(member, Expression.Convert(value, member.Type));
                return Expression.Lambda<Action<TModel, TValue?>>(assign, model, value).Compile();
            }

            if (member?.Member is FieldInfo field && !field.IsInitOnly)
            {
                var model = expression.Parameters[0];
                var value = Expression.Parameter(typeof(TValue), "value");
                var assign = Expression.Assign(member, Expression.Convert(value, member.Type));
                return Expression.Lambda<Action<TModel, TValue?>>(assign, model, value).Compile();
            }

            return null;
        }

        public static Action<TModel, TValue>? TryCreateNonNullableSetter<TModel, TValue>(Expression<Func<TModel, TValue>> expression)
        {
            var member = GetMemberExpression(expression.Body);

            if (member?.Member is PropertyInfo property && property.CanWrite)
            {
                var model = expression.Parameters[0];
                var value = Expression.Parameter(typeof(TValue), "value");
                var assign = Expression.Assign(member, Expression.Convert(value, member.Type));
                return Expression.Lambda<Action<TModel, TValue>>(assign, model, value).Compile();
            }

            if (member?.Member is FieldInfo field && !field.IsInitOnly)
            {
                var model = expression.Parameters[0];
                var value = Expression.Parameter(typeof(TValue), "value");
                var assign = Expression.Assign(member, Expression.Convert(value, member.Type));
                return Expression.Lambda<Action<TModel, TValue>>(assign, model, value).Compile();
            }

            return null;
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
