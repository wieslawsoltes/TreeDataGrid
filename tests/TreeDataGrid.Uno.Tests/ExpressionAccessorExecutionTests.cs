using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Uno.Controls;
using Uno.Data.Core.Parsers;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class ExpressionAccessorExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generated_setter_evaluates_the_current_nested_owner_once(bool field)
    {
        Expression<Func<Model, int>> expression = field ? x => x.Child!.Field : x => x.Child!.Number;
        var setter = TreeDataGridExpressionHelper.TryCreateNonNullableSetter(expression)!;
        var first = new Model();
        var second = new Model();
        var root = new Model { Next = first };
        Assert.Equal(0, root.ChildReads);
        setter(root, 17);
        Assert.Equal(1, root.ChildReads);
        Assert.Equal(17, field ? first.Field : first.Number);
        root.Next = second;
        setter(root, 29);
        Assert.Equal(2, root.ChildReads);
        Assert.Equal(17, field ? first.Field : first.Number);
        Assert.Equal(29, field ? second.Field : second.Number);
    }

    [Fact]
    public void Generated_setter_preserves_conversion_and_the_original_setter_exception()
    {
        Expression<Func<Model, object?>> expression = x => x.Number;
        var setter = TreeDataGridExpressionHelper.TryCreateNonNullableSetter(expression)!;
        var model = new Model();
        setter(model, 42);
        Assert.Equal(42, model.Number);
        Assert.Throws<InvalidCastException>(() => setter(model, "not an integer"));
        Assert.Equal(42, model.Number);
        var failure = new InvalidOperationException("original setter failure");
        model.WriteFailure = failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => setter(model, 99)));
        Assert.Equal(42, model.Number);
    }

    [Fact]
    public void Generated_setter_preserves_owner_getter_failure_and_null_owner_behavior()
    {
        Expression<Func<Model, int>> expression = x => x.Child!.Number;
        var setter = TreeDataGridExpressionHelper.TryCreateNonNullableSetter(expression)!;
        var root = new Model();
        Assert.Throws<NullReferenceException>(() => setter(root, 1));
        var failure = new InvalidOperationException("owner getter");
        root.ReadFailure = failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => setter(root, 2)));
        Assert.Equal(2, root.ChildReads);
    }

    [Fact]
    public void Nonassignable_expressions_still_have_no_generated_setter()
    {
        Assert.Null(TreeDataGridExpressionHelper.TryCreateNonNullableSetter<Model, int>(x => x.ReadOnly));
        Assert.Null(TreeDataGridExpressionHelper.TryCreateNonNullableSetter<Model, int>(x => x.Number + 1));
        Assert.Null(TreeDataGridExpressionHelper.TryCreateNonNullableSetter<Model, int>(x => x.ReadOnlyField));
    }

    [Fact]
    public void Generated_setter_does_not_capture_the_first_model()
    {
        var setter = TreeDataGridExpressionHelper.TryCreateNonNullableSetter<Model, int>(x => x.Number)!;
        var first = new Model();
        var second = new Model();
        setter(first, 11);
        setter(second, 22);
        Assert.Equal(11, first.Number);
        Assert.Equal(22, second.Number);
    }

    [Fact]
    public void Jit_owner_links_and_generated_writes_have_no_per_invocation_allocation()
    {
        var root = new Model { Next = new Model { Next = new Model() } };
        var links = ExpressionChainVisitor<Model>.Build(x => x.Child!.Child!.Number);
        var setter = TreeDataGridExpressionHelper.TryCreateNonNullableSetter<Model, int>(x => x.Number)!;
        for (var i = 0; i < 4096; ++i)
        {
            foreach (var link in links) _ = link(root);
            setter(root, i);
        }
        object? last = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
        {
            foreach (var link in links) last = link(root);
            setter(root, i);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(root.Next!.Next, last);
        Assert.Equal(4095, root.Number);
        // Interpreter fallback has different allocation behavior but must retain
        // identical semantics. This assertion applies only to JIT execution.
        if (RuntimeFeature.IsDynamicCodeCompiled) Assert.Equal(0L, allocated);
    }

    private sealed class Model
    {
        private int _number;
        public Model? Next;
        public int ChildReads;
        public int Field = 0;
        public readonly int ReadOnlyField = 7;
        public Exception? ReadFailure;
        public Exception? WriteFailure;
        public int ReadOnly => 1;
        public Model? Child
        {
            get
            {
                ++ChildReads;
                if (ReadFailure is { } failure) throw failure;
                return Next;
            }
        }
        public int Number
        {
            get => _number;
            set
            {
                if (WriteFailure is { } failure) throw failure;
                _number = value;
            }
        }
    }
}
