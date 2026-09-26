using System;
using System.ComponentModel;
using Uno.Data;
using Uno.Experimental.Data;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public instruction snapshots consumed by actual native and trimmed cells.</summary>
internal static class TypedBindingPlanRuntimeChecks
{
    internal static void Run()
    {
        var a = new Model { Number = 17 }; var b = new Model { Number = 29 };
        var descriptor = TypedBinding<Model>.TwoWay(x => x.Number, (x, v) => x.Number = v);
        using var ea = descriptor.Instance(a); using var eb = descriptor.Instance(b);
        using var first = new UI.TextCell<int>(ea, false);
        using var second = new UI.TextCell<int>(eb, false);
        Check(first.Value == 17 && second.Value == 29 && a.Subscribers == 1 && b.Subscribers == 1,
            "Repeated descriptor creation lost independent roots or observations.");
        first.Value = 43;
        Check(a.Number == 43 && b.Number == 29 && second.Value == 29, "One expression wrote through a different root.");
        descriptor.Read = x => x.Number + 100;
        using var ec = descriptor.Instance(b); using var third = new UI.TextCell<int>(ec, false);
        Check(third.Value == 129 && second.Value == 29, "Editing a descriptor modified an existing expression.");
        b.Number = 31;
        Check(second.Value == 31 && third.Value == 131, "Descriptor generations share mutable value state.");
        first.Dispose(); ea.Dispose();
        Check(a.Subscribers == 0 && b.Subscribers == 2, "Disposal retired another expression or leaked the old root.");
        third.Dispose(); ec.Dispose(); second.Dispose(); eb.Dispose();
        Check(b.Subscribers == 0, "Repeated expressions left root subscriptions behind.");

        descriptor.Read = x => x.Number;
        var link = descriptor.Links![0];
        using var old = descriptor.Instance(a);
        descriptor.Links[0] = null!;
        Exception? failure = null;
        try { descriptor.Instance(b); } catch (Exception error) { failure = error; }
        Check(failure is ArgumentException, "Invalid mutated links were not rejected.");
        descriptor.Links[0] = link;
        using var recovered = descriptor.Instance(b);
        using var oldCell = new UI.TextCell<int>(old, false);
        using var recoveredCell = new UI.TextCell<int>(recovered, false);
        Check(oldCell.Value == a.Number && recoveredCell.Value == b.Number,
            "A failed snapshot creation corrupted an earlier expression or prevented recovery.");
        oldCell.Dispose(); recoveredCell.Dispose(); old.Dispose(); recovered.Dispose();
        Check(a.Subscribers == 0 && b.Subscribers == 0, "Recovered expressions leaked observers.");

        using var once = descriptor.Instance(a, BindingMode.OneTime);
        using var onceCell = new UI.TextCell<int>(once, true);
        var captured = onceCell.Value; a.Number = 61;
        Check(onceCell.Value == captured && a.Subscribers == 0, "One-time mode reused observing instructions.");
        var booleanDescriptor = TypedBinding<Model>.TwoWay(x => x.Flag, (x, v) => x.Flag = v);
        using var ba = booleanDescriptor.Instance(a); using var bb = booleanDescriptor.Instance(b);
        using var ca = new UI.CheckBoxCell(ba, false, true); using var cb = new UI.CheckBoxCell(bb, false, true);
        ca.Value = true; cb.Value = null;
        Check(a.Flag == true && b.Flag is null, "Nullable checkbox snapshots lost independent writeback.");
        ca.Dispose(); cb.Dispose(); ba.Dispose(); bb.Dispose();
        Check(a.Subscribers == 0 && b.Subscribers == 0, "Typed checkbox snapshots leaked observers.");
        Console.WriteLine("UNO_RUNTIME_BINDING_PLAN_PASSED: independent roots/writers, descriptor generations, in-place link failure/recovery, one-time mode, text/nullable-checkbox consumers and cleanup");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Model : INotifyPropertyChanged
    {
        private int _number; private bool? _flag;
        private PropertyChangedEventHandler? _handlers;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public int Number { get => _number; set { _number = value; _handlers?.Invoke(this, new(nameof(Number))); } }
        public bool? Flag { get => _flag; set { _flag = value; _handlers?.Invoke(this, new(nameof(Flag))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
}
