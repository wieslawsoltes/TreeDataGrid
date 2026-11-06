using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using Uno.Data.Core.Parsers;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;
using Xunit;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnConstructionExecutionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Ordinary_member_paths_share_the_initial_reader_but_not_descriptor_mutation(bool nested, bool writable)
    {
        Expression<Func<Node, int>> getter = nested ? x => x.Child!.Number : x => x.Number;
        Action<Node, int>? setter = writable ? (x, v) => { if (nested) x.Child!.Number = v; else x.Number = v; } : null;
        var column = new Column(getter, setter);
        Assert.Same(column.Binding.Read, column.ValueSelector);
        Assert.Same(setter, column.Binding.Write);
        var root = new Node { Number = 17, Child = new() { Number = 29 } };
        var expected = nested ? 29 : 17;
        Assert.Equal(expected, column.ValueSelector(root));
        column.Binding.Read = static _ => 101;
        Assert.Equal(expected, column.ValueSelector(root));
        using var value = column.Bind(root);
        var observer = new Recorder();
        using var subscription = value.Subscribe(observer);
        Assert.Equal(101, observer.Last);
        Assert.Equal(expected, column.ValueSelector(root));
    }

    [Fact]
    public void Owner_arrays_are_independent_while_the_reference_root_link_is_stateless()
    {
        var first = ExpressionChainVisitor<Node>.Build(x => x.Child!.Number);
        var second = ExpressionChainVisitor<Node>.Build(x => x.Child!.Child!.Number);
        Assert.NotSame(first, second);
        Assert.Same(first[0], second[0]);
        var a = new Node(); var b = new Node();
        Assert.Same(a, first[0](a)); Assert.Same(b, second[0](b));
        Assert.Null(first[0](null!));
        first[0] = _ => a;
        Assert.Same(b, second[0](b));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Non_member_expressions_keep_independent_reader_construction(int kind)
    {
        var captured = new Node { Number = 31 };
        Expression<Func<Node, int>> getter = kind switch
        {
            0 => x => x.ReadNumber(),
            1 => x => x.Number + 1,
            _ => x => captured.Number,
        };
        var column = new Column(getter);
        Assert.NotSame(column.Binding.Read, column.ValueSelector);
        var node = new Node { Number = 7 };
        Assert.Equal(getter.Compile()(node), column.ValueSelector(node));
        Assert.Equal(column.ValueSelector(node), column.Binding.Read!(node));
    }

    [Fact]
    public void Stateful_extension_reduction_keeps_the_original_two_reader_compilation_sequence()
    {
        var expectedReduction = new List<int>();
        var actualReduction = new List<int>();
        var parameter = Expression.Parameter(typeof(Node), "x");
        var expectedExpression = Expression.Lambda<Func<Node, int>>(new ChangingExpression(expectedReduction), parameter);
        var actualExpression = Expression.Lambda<Func<Node, int>>(new ChangingExpression(actualReduction), parameter);
        var expectedSelector = expectedExpression.Compile(preferInterpretation: !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled);
        var expectedBinding = TypedBinding<Node>.OneWay(expectedExpression);
        var column = new Column(actualExpression);
        Assert.Equal(expectedReduction, actualReduction);
        Assert.Equal(expectedSelector(new()), column.ValueSelector(new()));
        Assert.Equal(expectedBinding.Read!(new()), column.Binding.Read!(new()));
        Assert.NotSame(column.Binding.Read, column.ValueSelector);
    }

    [Fact]
    public void Throwing_extension_reduction_preserves_the_original_exception()
    {
        var failure = new InvalidOperationException("reduction");
        var expression = Expression.Lambda<Func<Node, int>>(new ThrowingExpression(failure), Expression.Parameter(typeof(Node)));
        Assert.Same(failure, Record.Exception(() => new Column(expression)));
    }

    [Fact]
    public void Explicit_delegate_and_binding_overload_remains_independent()
    {
        Func<Node, int> selector = static x => -x.Number;
        var binding = TypedBinding<Node>.OneWay(x => x.Number);
        var column = new Column(selector, binding);
        Assert.Same(selector, column.ValueSelector);
        Assert.Same(binding, column.Binding);
        Assert.Equal(-7, column.ValueSelector(new() { Number = 7 }));
        Assert.Equal(7, column.Binding.Read!(new() { Number = 7 }));
    }

    [Fact]
    public void Null_getter_keeps_its_public_parameter_name()
    {
        Assert.Equal("getter", Assert.Throws<ArgumentNullException>(() => new Column((Expression<Func<Node, int>>)null!)).ParamName);
    }

    private sealed class Column : U.ColumnBase<Node, int>
    {
        public Column(Expression<Func<Node, int>> getter, Action<Node, int>? setter = null) : base("Number", getter, setter, null, new()) { }
        public Column(Func<Node, int> selector, TypedBinding<Node, int> binding) : base("Number", selector, binding, null, null) { }
        public TypedBindingExpression<Node, int> Bind(Node root) => CreateBindingExpression(root);
        public override U.ICell CreateCell(TreeDataGridCore.Models.IRow<Node> row) => new U.TextCell<int>(ValueSelector(row.Model));
    }
    private sealed class ChangingExpression(List<int> reductions) : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(int);
        public override bool CanReduce => true;
        public override Expression Reduce() { var result = reductions.Count + 1; reductions.Add(result); return Constant(result); }
    }
    private sealed class ThrowingExpression(Exception error) : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(int);
        public override bool CanReduce => true;
        public override Expression Reduce() => throw error;
    }
    private sealed class Recorder : IObserver<global::Uno.Data.BindingValue<int>>
    {
        public int Last;
        public void OnNext(global::Uno.Data.BindingValue<int> value) { if (value.HasValue) Last = value.Value; }
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
    }
    private sealed class Node
    {
        public Node? Child { get; set; }
        public int Number { get; set; }
        public int ReadNumber() => Number;
    }
}
