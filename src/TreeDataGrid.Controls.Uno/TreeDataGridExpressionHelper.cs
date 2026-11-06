using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Uno.Controls;

internal static class TreeDataGridExpressionHelper
{
    public static string? TryGetMemberName<TModel, TValue>(Expression<Func<TModel, TValue>> expression) =>
        GetMember(expression.Body)?.Member.Name;

    public static Action<TModel, TValue?>? TryCreateSetter<TModel, TValue>(Expression<Func<TModel, TValue?>> expression) =>
        TryCreateNonNullableSetter(expression);

    public static Action<TModel, TValue>? TryCreateNonNullableSetter<TModel, TValue>(Expression<Func<TModel, TValue>> expression)
    {
        var member = GetMember(expression.Body);
        if (member?.Member is PropertyInfo { CanWrite: true } or FieldInfo { IsInitOnly: false, IsLiteral: false })
        {
            var value = Expression.Parameter(typeof(TValue), "value");
            var assign = Expression.Assign(member, Expression.Convert(value, member.Type));
            return Expression.Lambda<Action<TModel, TValue>>(assign, expression.Parameters[0], value)
                .Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled);
        }
        return null;
    }

    private static MemberExpression? GetMember(Expression expression) => expression switch
    {
        MemberExpression member => member,
        UnaryExpression { NodeType: ExpressionType.Convert } unary => GetMember(unary.Operand),
        _ => null,
    };
}
