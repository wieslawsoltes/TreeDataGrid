using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Primitives;
using Uno.Data;
using Uno.Experimental.Data.Core;

namespace TreeDataGridUnoSample;

/// <summary>Observable-root activation ownership through borrowed public cell models and actual native controls.</summary>
internal static class TypedRootLifetimeRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        await TextAsync(page);
        await CheckBoxAsync(page);
        Console.WriteLine("UNO_RUNTIME_TYPED_ROOT_LIFETIME_PASSED: text/nullable-checkbox native cells, same-control reactivation, retired value/null/error rejection, native scalar writeback, null fallback/recovery, final-root observation and exact subscription ownership");
    }

    private static async Task TextAsync(MainPage page)
    {
        var previous = page.Content;
        var roots = new Roots();
        var old = new Model { Text = "Old" };
        var current = new Model { Text = "Current" };
        var writes = 0;
        using var expression = new TypedBindingExpression<Model, string>(roots,
            static model => model.Text, (model, text) => { ++writes; model.Text = text; },
            [static model => model], "Missing root");
        var factory = new TreeDataGridElementFactory();
        var control = new TreeDataGridTextCell();
        TextCell<string>? modelCell = null;
        try
        {
            modelCell = new TextCell<string>(expression, isReadOnly: false);
            var obsolete = roots.Current;
            control.Realize(factory, null, modelCell, 0, 0);
            obsolete.Next(old);
            page.Content = control;
            await Settle();
            Check(control.Value == "Old" && old.Subscribers == 1 && roots.ActiveCount == 1,
                "The first native text activation did not receive its root.");
            control.Unrealize();
            Check(old.Subscribers == 1, "The native control disposed a caller-owned text model.");
            modelCell.Dispose();
            modelCell = null;
            Check(old.Subscribers == 0 && roots.ActiveCount == 0,
                "The final text-model subscriber did not release the root operation.");
            obsolete.Fail(new InvalidOperationException("Already retired text stream"));

            modelCell = new TextCell<string>(expression, isReadOnly: false);
            control.Realize(factory, null, modelCell, 2, 7);
            roots.Current.Next(current);
            await Settle();
            Check(control.Value == "Current" && current.Subscribers == 1,
                "A retired error poisoned text reactivation.");
            obsolete.Next(old);
            obsolete.Next(null);
            obsolete.Fail(new InvalidOperationException("Old activation error"));
            Check(control.Value == "Current" && ReferenceEquals(control.Model, modelCell) &&
                control.ColumnIndex == 2 && control.RowIndex == 7 &&
                old.Subscribers == 0 && current.Subscribers == 1 && roots.ActiveCount == 1,
                "A captured old callback replaced native text state or terminated the current activation.");

            control.Value = "Native scalar write";
            Check(current.Text == "Native scalar write" && old.Text == "Old" && writes == 1,
                "Native scalar writeback targeted a retired root or echoed twice.");
            roots.Current.Next(null);
            Check(control.Value == "Missing root" && current.Subscribers == 0,
                "Current null-root publication did not release its model and render fallback.");
            roots.Current.Next(current);
            Check(control.Value == "Native scalar write" && current.Subscribers == 1,
                "The current null root could not recover.");
            roots.Current.Complete();
            current.Text = "After stream completion";
            Check(control.Value == "After stream completion" && current.Subscribers == 1,
                "Completing the current root stream froze its final model.");
        }
        finally
        {
            try { control.Unrealize(); }
            finally
            {
                try { modelCell?.Dispose(); }
                finally { page.Content = previous; }
            }
        }
        Check(old.Subscribers == 0 && current.Subscribers == 0 && roots.ActiveCount == 0 && roots.DisposeCalls == 0,
            "Text cleanup retained a source handler or disposed the caller-owned observable.");
        async Task Settle() { await Task.Delay(75); control.UpdateLayout(); }
    }

    private static async Task CheckBoxAsync(MainPage page)
    {
        var previous = page.Content;
        var roots = new Roots();
        var old = new Model { Flag = true };
        var current = new Model { Flag = false };
        var writes = 0;
        using var expression = new TypedBindingExpression<Model, bool?>(roots,
            static model => model.Flag, (model, value) => { ++writes; model.Flag = value; },
            [static model => model], new Optional<bool?>(null));
        var factory = new TreeDataGridElementFactory();
        var control = new TreeDataGridCheckBoxCell();
        CheckBoxCell? modelCell = null;
        try
        {
            modelCell = new CheckBoxCell(expression, isReadOnly: false, isThreeState: true);
            var obsolete = roots.Current;
            control.Realize(factory, null, modelCell, 0, 0);
            obsolete.Next(old);
            page.Content = control;
            await Settle();
            Check(control.Value == true && control.IsThreeState && old.Subscribers == 1,
                "Nullable checkbox root did not initialize native state.");
            control.Unrealize();
            Check(old.Subscribers == 1, "The native checkbox disposed its borrowed cell model.");
            modelCell.Dispose();
            modelCell = new CheckBoxCell(expression, isReadOnly: false, isThreeState: true);
            control.Realize(factory, null, modelCell, 1, 9);
            roots.Current.Next(current);
            obsolete.Next(old);
            obsolete.Next(null);
            obsolete.Fail(new InvalidOperationException("Retired checkbox stream"));
            await Settle();
            Check(control.Value == false && control.IsThreeState && old.Subscribers == 0 &&
                current.Subscribers == 1 && roots.ActiveCount == 1,
                "Retired checkbox callbacks changed current state or ownership.");
            control.Value = null;
            Check(current.Flag is null && old.Flag == true && writes == 1,
                "Native nullable-checkbox writeback used a retired source or lost null.");
            current.Flag = true;
            Check(control.Value == true && writes == 1,
                "External checkbox updates did not reach the current activation without writeback echo.");
            roots.Current.Next(null);
            Check(control.Value is null && current.Subscribers == 0,
                "Null checkbox root did not display its nullable fallback and detach.");
            roots.Current.Next(current);
            Check(control.Value == true && current.Subscribers == 1,
                "Nullable checkbox binding did not recover after a missing root.");
        }
        finally
        {
            try { control.Unrealize(); }
            finally
            {
                try { modelCell?.Dispose(); }
                finally { page.Content = previous; }
            }
        }
        Check(old.Subscribers == 0 && current.Subscribers == 0 && roots.ActiveCount == 0 && roots.DisposeCalls == 0,
            "Checkbox cleanup retained a model or disposed the caller-owned observable.");
        async Task Settle() { await Task.Delay(75); control.UpdateLayout(); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs TextChanged = new(nameof(Text));
        private static readonly PropertyChangedEventArgs FlagChanged = new(nameof(Flag));
        private PropertyChangedEventHandler? _changed;
        private string _text = string.Empty;
        private bool? _flag;
        internal int Subscribers;
        public string Text { get => _text; set { _text = value; _changed?.Invoke(this, TextChanged); } }
        public bool? Flag { get => _flag; set { _flag = value; _changed?.Invoke(this, FlagChanged); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class Roots : IObservable<Model?>, IDisposable
    {
        private readonly List<Session> _sessions = new();
        internal Session Current => _sessions[^1];
        internal int DisposeCalls;
        internal int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var session in _sessions) if (session.Active) ++count;
                return count;
            }
        }
        public IDisposable Subscribe(IObserver<Model?> observer)
        {
            var session = new Session(observer);
            _sessions.Add(session);
            return session;
        }
        public void Dispose() => ++DisposeCalls;
    }
    private sealed class Session(IObserver<Model?> observer) : IDisposable
    {
        internal bool Active = true;
        // Replay an already captured callback even after its token is disposed.
        internal void Next(Model? value) => observer.OnNext(value);
        internal void Fail(Exception error) => observer.OnError(error);
        internal void Complete() => observer.OnCompleted();
        public void Dispose() => Active = false;
    }
}
