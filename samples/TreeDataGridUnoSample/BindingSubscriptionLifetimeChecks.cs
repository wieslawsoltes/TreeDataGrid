using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Native = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Shared public-API cases executed by unit, native and trimmed consumers.</summary>
internal static class BindingSubscriptionLifetimeChecks
{
    private static readonly string[] Cases =
    [
        "independent-removals", "nested-removals", "property-add-failure", "collection-add-failure",
        "add-and-cleanup-errors", "constructor-and-cleanup-errors", "reentrant-add-retirement",
        "recursive-dispose", "duplicate-owner", "failed-retarget-recovery",
    ];

    public static void RunAll()
    {
        foreach (var name in Cases) RunCase(name);
        Console.WriteLine($"UNO_RUNTIME_BINDING_SUBSCRIPTION_LIFETIME_PASSED: cases={Cases.Length}; independent cleanup, original/cleanup failures, attempted-add rollback, nested owners, deduplication, retirement and reuse");
    }
    public static void RunCase(string name)
    {
        switch (name)
        {
            case "independent-removals": IndependentRemovals(); break;
            case "nested-removals": NestedRemovals(); break;
            case "property-add-failure": AddFailure(property: true); break;
            case "collection-add-failure": AddFailure(property: false); break;
            case "add-and-cleanup-errors": AddAndCleanupErrors(); break;
            case "constructor-and-cleanup-errors": ConstructorAndCleanupErrors(); break;
            case "reentrant-add-retirement": ReentrantAddRetirement(); break;
            case "recursive-dispose": RecursiveDispose(); break;
            case "duplicate-owner": DuplicateOwner(); break;
            case "failed-retarget-recovery": FailedRetargetRecovery(); break;
            default: throw new ArgumentException("Unknown binding cleanup case: " + name, nameof(name));
        }
        Console.WriteLine("UNO_BINDING_SUBSCRIPTION_CASE_PASSED: " + name);
    }

    private static void IndependentRemovals()
    {
        var node = new Node { Number = 7 };
        using var source = new FlatTreeDataGridSource<Node>([node]);
        using var column = Column();
        var cell = column.CreateCell(source.Rows[0]);
        var failure = new InvalidOperationException("property removal");
        node.PropertyRemoveError = failure;
        Check(ReferenceEquals(Capture(cell.Dispose), failure), "A single remove failure lost its exception identity.");
        Clean(node);
        Check(node.PropertyRemovals == 1 && node.CollectionRemovals == 1, "One failed removal skipped the other notification interface.");
        cell.Dispose();
        Check(node.PropertyRemovals == 1 && node.CollectionRemovals == 1, "Repeated disposal removed subscriptions twice.");
    }
    private static void NestedRemovals()
    {
        var child = new Node { Number = 7 };
        var root = new Node { Child = child };
        using var source = new FlatTreeDataGridSource<Node>([root]);
        using var column = Column(nested: true);
        var cell = column.CreateCell(source.Rows[0]);
        var p1 = root.PropertyRemoveError = new InvalidOperationException("root property");
        var c1 = root.CollectionRemoveError = new InvalidOperationException("root collection");
        var p2 = child.PropertyRemoveError = new InvalidOperationException("child property");
        var c2 = child.CollectionRemoveError = new InvalidOperationException("child collection");
        Failures(Capture(cell.Dispose), p1, c1, p2, c2);
        Clean(root); Clean(child);
        cell.Dispose();
    }
    private static void AddFailure(bool property)
    {
        var failure = new InvalidOperationException("add after attaching");
        var node = new Node { PropertyAddError = property ? failure : null, CollectionAddError = property ? null : failure };
        using var source = new FlatTreeDataGridSource<Node>([node]);
        using var column = Column();
        Check(ReferenceEquals(Capture(() => column.CreateCell(source.Rows[0])), failure), "An add failure lost its original exception.");
        Clean(node);
        Check(node.PropertyRemovals == 1 && node.CollectionRemovals == (property ? 0 : 1),
            "Failed attachment did not roll back exactly its attempted event adds.");
    }
    private static void AddAndCleanupErrors()
    {
        var add = new InvalidOperationException("collection add");
        var property = new InvalidOperationException("property rollback");
        var collection = new InvalidOperationException("collection rollback");
        var node = new Node { CollectionAddError = add, PropertyRemoveError = property, CollectionRemoveError = collection };
        using var source = new FlatTreeDataGridSource<Node>([node]);
        using var column = Column();
        Failures(Capture(() => column.CreateCell(source.Rows[0])), add, property, collection);
        Clean(node);
    }
    private static void ConstructorAndCleanupErrors()
    {
        var add = new InvalidOperationException("child initialization");
        var property = new InvalidOperationException("root property cleanup");
        var collection = new InvalidOperationException("root collection cleanup");
        var child = new Node { PropertyAddError = add };
        var root = new Node { Child = child, PropertyRemoveError = property, CollectionRemoveError = collection };
        using var source = new FlatTreeDataGridSource<Node>([root]);
        using var column = Column(nested: true);
        Failures(Capture(() => column.CreateCell(source.Rows[0])), add, property, collection);
        Clean(root); Clean(child);
    }
    private static void ReentrantAddRetirement()
    {
        var old = new Node { Number = 7 };
        var next = new Node { Number = 9 };
        using var source = new FlatTreeDataGridSource<Node>([old, next]);
        using var column = Column();
        var cell = column.CreateCell(source.Rows[0]);
        next.OnPropertyAdd = cell.Dispose;
        column.TryReuseCell(cell, (IRow<Node>)source.Rows[1]);
        Clean(old); Clean(next);
        Check(Capture(() => cell.Write(42)) is ObjectDisposedException, "An event-add callback left a retired cell writable.");
        Check(next.Number == 9, "A retired cell changed the replacement row.");
        cell.Dispose();
    }
    private static void RecursiveDispose()
    {
        var node = new Node();
        using var source = new FlatTreeDataGridSource<Node>([node]);
        using var column = Column();
        var cell = column.CreateCell(source.Rows[0]);
        var failure = node.PropertyRemoveError = new InvalidOperationException("recursive cleanup");
        node.OnPropertyRemove = cell.Dispose;
        Check(ReferenceEquals(Capture(cell.Dispose), failure), "Recursive disposal changed the cleanup failure.");
        Clean(node);
        Check(node.PropertyRemovals == 1 && node.CollectionRemovals == 1, "Recursive disposal repeated or skipped cleanup.");
    }
    private static void DuplicateOwner()
    {
        var node = new Node { Number = 17 };
        node.Child = node;
        using var source = new FlatTreeDataGridSource<Node>([node]);
        using var column = Column(nested: true);
        var cell = column.CreateCell(source.Rows[0]);
        Check(node.PropertySubscribers == 1 && node.CollectionSubscribers == 1, "The same expression owner was observed twice.");
        cell.Dispose();
        Clean(node);
        Check(node.PropertyRemovals == 1 && node.CollectionRemovals == 1, "A repeated owner was unsubscribed twice.");
    }
    private static void FailedRetargetRecovery()
    {
        var old = new Node { Number = 7 };
        var next = new Node { Number = 9 };
        using var source = new FlatTreeDataGridSource<Node>([old, next]);
        using var column = Column();
        var cell = column.CreateCell(source.Rows[0]);
        var failure = old.PropertyRemoveError = new InvalidOperationException("retarget cleanup");
        Check(ReferenceEquals(Capture(() => column.TryReuseCell(cell, (IRow<Node>)source.Rows[1])), failure), "Retarget lost the old-owner cleanup error.");
        cell.Dispose();
        Clean(old); Clean(next);
        var fresh = column.CreateCell(source.Rows[1]);
        try
        {
            Check(Equals(fresh.Value, 9), "Failed retirement poisoned subsequent column realization.");
            next.Number = 11;
            Check(Equals(fresh.Value, 11), "Subsequent realization lost its live binding.");
        }
        finally { fresh.Dispose(); }
        Clean(next);
    }

    private static Native.TextColumn<Node, int> Column(bool nested = false)
    {
        Expression<Func<Node, int>> getter = nested ? node => node.Child!.Number : node => node.Number;
        return new("Number", getter, (node, value) => (nested ? node.Child! : node).Number = value);
    }
    private static void Clean(Node node) => Check(node.PropertySubscribers == 0 && node.CollectionSubscribers == 0,
        $"The cell retained notification handlers: property={node.PropertySubscribers}; collection={node.CollectionSubscribers}.");
    private static Exception Capture(Action action)
    {
        try { action(); }
        catch (Exception error) { return error; }
        throw new InvalidOperationException("Expected the operation to report its failure.");
    }
    private static void Failures(Exception actual, params Exception[] expected)
    {
        var observed = OrderedFailures(actual).ToArray();
        Check(observed.Length == expected.Length && observed.Zip(expected).All(pair => ReferenceEquals(pair.First, pair.Second)),
            "Primary and cleanup exception identities/order were not preserved.");
    }
    private static IEnumerable<Exception> OrderedFailures(Exception error)
    {
        if (error is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
                foreach (var failure in OrderedFailures(inner)) yield return failure;
        }
        else yield return error;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Node : INotifyPropertyChanged, INotifyCollectionChanged
    {
        private PropertyChangedEventHandler? _property;
        private NotifyCollectionChangedEventHandler? _collection;
        private int _number;
        private Node? _child;
        private static readonly PropertyChangedEventArgs NumberChanged = new(nameof(Number));
        private static readonly PropertyChangedEventArgs ChildChanged = new(nameof(Child));
        public int PropertySubscribers => _property?.GetInvocationList().Length ?? 0;
        public int CollectionSubscribers => _collection?.GetInvocationList().Length ?? 0;
        public int PropertyRemovals, CollectionRemovals;
        public Exception? PropertyAddError, CollectionAddError, PropertyRemoveError, CollectionRemoveError;
        public Action? OnPropertyAdd, OnPropertyRemove;
        public int Number { get => _number; set { _number = value; _property?.Invoke(this, NumberChanged); } }
        public Node? Child { get => _child; set { _child = value; _property?.Invoke(this, ChildChanged); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _property += value;
                OnPropertyAdd?.Invoke();
                if (PropertyAddError is { } error) throw error;
            }
            remove
            {
                _property -= value;
                ++PropertyRemovals;
                OnPropertyRemove?.Invoke();
                if (PropertyRemoveError is { } error) throw error;
            }
        }
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _collection += value; if (CollectionAddError is { } error) throw error; }
            remove { _collection -= value; ++CollectionRemovals; if (CollectionRemoveError is { } error) throw error; }
        }
    }
}
