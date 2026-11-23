using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;
using A = Avalonia.Data.Core.Parsers.ExpressionChainVisitor<TreeDataGrid.Parity.Tests.ExpressionAccessorParityTests.Node>;
using U = Uno.Data.Core.Parsers.ExpressionChainVisitor<TreeDataGrid.Parity.Tests.ExpressionAccessorParityTests.Node>;

namespace TreeDataGrid.Parity.Tests;

public sealed class ExpressionAccessorParityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Owner_links_keep_the_reference_traversal_identity_and_evaluation_order(int variant)
    {
        Expression<Func<Node, string?>> expression = variant switch
        {
            0 => x => x.Name,
            1 => x => x.Child!.Name,
            2 => x => x.Child!.Child!.Name,
            3 => x => x.GetChild()!.Name,
            4 => x => x.Child!.GetChild()!.Name,
            _ => x => ((Node)(object)x.Child!).Name,
        };
        var trace = new List<string>();
        var root = new Node("root", trace)
        {
            Next = new Node("child", trace) { Next = new Node("leaf", trace) },
        };
        var reference = A.Build(expression);
        var native = U.Build(expression);
        Assert.Empty(trace);
        Assert.Equal(reference.Length, native.Length);
        for (var i = 0; i < reference.Length; ++i)
        {
            trace.Clear();
            var expected = reference[i](root);
            var calls = trace.ToArray();
            trace.Clear();
            Assert.Same(expected, native[i](root));
            Assert.Equal(calls, trace);
        }
        root.Next = new Node("replacement", trace) { Next = new Node("new leaf", trace) };
        for (var i = 0; i < reference.Length; ++i) Assert.Same(reference[i](root), native[i](root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Owner_links_keep_original_failures_instead_of_reflection_wrappers(bool failGetter)
    {
        Expression<Func<Node, string?>> expression = x => x.Child!.Child!.Name;
        var expected = new InvalidOperationException("original getter failure");
        var root = new Node("root", new()) { Failure = failGetter ? expected : null };
        var reference = A.Build(expression);
        var native = U.Build(expression);
        Assert.Equal(3, native.Length);
        var a = Record.Exception(() => reference[2](root));
        var u = Record.Exception(() => native[2](root));
        Assert.NotNull(a);
        Assert.NotNull(u);
        Assert.Equal(a.GetType(), u.GetType());
        if (failGetter) { Assert.Same(expected, a); Assert.Same(expected, u); }
        else Assert.IsType<NullReferenceException>(u);
    }

    public sealed class Node(string name, List<string> trace)
    {
        public Node? Next;
        public Exception? Failure;
        public string Name => name;
        public Node? Child
        {
            get
            {
                trace.Add(name + ".Child");
                if (Failure is { } failure) throw failure;
                return Next;
            }
        }
        public Node? GetChild()
        {
            trace.Add(name + ".GetChild");
            if (Failure is { } failure) throw failure;
            return Next;
        }
    }
}
