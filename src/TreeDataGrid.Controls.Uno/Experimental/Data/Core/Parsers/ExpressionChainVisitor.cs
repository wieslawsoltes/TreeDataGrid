// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) AvaloniaUI OÜ. All Rights Reserved.
// See THIRD-PARTY-NOTICES.md, build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

#nullable enable

namespace Uno.Data.Core.Parsers
{
    public class ExpressionChainVisitor<TIn> : ExpressionVisitor
    {
        private readonly LambdaExpression _rootExpression;
        private readonly List<Func<TIn, object>> _links = new();
        private Expression? _head;

        public ExpressionChainVisitor(LambdaExpression expression)
        {
            _rootExpression = expression;
        }

        /// <summary>
        /// Builds an array of delegates which return the intermediate objects in the expression chain.
        /// <example>
        /// For example, if the expression is <c>x => x.Foo.Bar.Baz</c> then the links will be:
        /// <code>
        ///  - x => x
        ///  - x => x.Foo
        ///  - x => x.Foo.Bar
        /// </code>
        /// There is no delegate for the final property of the expression <c>x => x.Foo.Bar.Baz</c>.
        /// </example>
        /// </summary>
    
        public static Func<TIn, object>[] Build<TOut>(Expression<Func<TIn, TOut>> expression)
        {
            var visitor = new ExpressionChainVisitor<TIn>(expression);
            visitor.Visit(expression);
            return visitor._links.ToArray();
        }

        private Func<TIn, object> CreateLink(Expression owner)
        {
            // A reference root is already the owner. Do not compile an
            // identity DynamicMethod or interpreter for every new chain.
            if (ReferenceEquals(owner, _rootExpression.Parameters[0]) && !owner.Type.IsValueType)
                return static root => root!;
            return Expression.Lambda<Func<TIn, object>>(owner, _rootExpression.Parameters)
                .Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var result = base.VisitBinary(node);
            if (node.Left == _head)
                _head = node;
            return result;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var result = base.VisitMember(node);

            if (node.Expression is not null &&
                node.Expression == _head &&
                node.Expression.Type.IsValueType == false)
            {
                _links.Add(CreateLink(node.Expression));
                _head = node;
            }

            return result;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var result = base.VisitMethodCall(node);

            if (node.Object is not null &&
                node.Object == _head &&
                node.Type.IsValueType == false)
            {
                _links.Add(CreateLink(node.Object));
                _head = node;
            }

            return result;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _rootExpression.Parameters[0])
                _head = node;
            return base.VisitParameter(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            var result = base.VisitUnary(node);
            if (node.Operand == _head)
                _head = node;
            return result;
        }
    }

}
