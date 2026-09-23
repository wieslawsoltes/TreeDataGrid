using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace TreeDataGridUnoSample;

/// <summary>Public package-consumer binding contracts; also executed after browser trimming.</summary>
internal static class BindingWriteContractRuntimeChecks
{
    internal static void Run()
    {
        RecycleDuringConversion(1);
        RecycleDuringConversion(0);
        RetireDuringConversion(false);
        RetireDuringConversion(true);
        NestedAssignment();
        DiagnosticReplacement();
        DualFailure();
        Console.WriteLine("UNO_RUNTIME_BINDING_WRITE_CONTRACT_PASSED: cases=7; real pool reuse, same-model reuse, retirement, nested conversion writes, diagnostic replacement, ordered failures and source ownership");
    }

    private static void RecycleDuringConversion(int row)
    {
        using var fixture = new Fixture();
        var original = fixture.Cell;
        fixture.Cell.Write(new Input(() =>
        {
            fixture.RecycleTo(row);
            Check(ReferenceEquals(original, fixture.Cell), "The fixture did not exercise real bound-cell pool reuse.");
            return 99;
        }));
        Check(fixture.Writes == 0 && fixture.Models[0].Value == 1 && fixture.Models[1].Value == 2,
            "A converted value crossed the recycled binding's lifetime.");
        fixture.Cell.Write("42");
        Check(fixture.Models[row].Value == 42 && fixture.Writes == 1, "Current conversion failed after recycling.");
    }

    private static void RetireDuringConversion(bool dispose)
    {
        using var fixture = new Fixture();
        fixture.Cell.Write(new Input(() =>
        {
            if (dispose) fixture.Cell.Dispose();
            else fixture.RecycleOnly();
            return 99;
        }));
        Check(fixture.Writes == 0 && fixture.Models.All(model => model.Subscribers == 0),
            "Retired conversion wrote a value or retained model observation.");
        Check(fixture.Source.Rows.Count == 2, "Retiring a view value disposed its borrowed Core source.");
    }

    private static void NestedAssignment()
    {
        using var fixture = new Fixture();
        fixture.Cell.Write(new Input(() => { fixture.Cell.Write("42"); return 99; }));
        Check(fixture.Models[0].Value == 42 && Equals(fixture.Cell.Value, 42) && fixture.Writes == 1,
            "An older conversion overwrote a newer nested write.");
    }

    private static void DiagnosticReplacement()
    {
        using var fixture = new Fixture();
        var first = new InvalidOperationException("First diagnostic");
        var second = new InvalidOperationException("Second diagnostic");
        var model = fixture.Models[0];
        model.Failure = first;
        model.Notify();
        Check(ReferenceEquals(fixture.Cell.Error, first), "The first error was not published.");
        var events = new List<string?>();
        PropertyChangedEventHandler observer = (_, args) => events.Add(args.PropertyName);
        fixture.Cell.PropertyChanged += observer;
        try
        {
            model.Failure = second;
            model.Notify();
            Check(ReferenceEquals(fixture.Cell.Error, second) && events.SequenceEqual(new[] { "Value", "Error" }),
                "A changed same-type diagnostic did not notify through the public CellValue contract.");
            events.Clear();
            model.Notify();
            Check(events.Count == 0, "An identical diagnostic generated duplicate notifications.");
            model.Failure = null;
            model.Notify();
            Check(fixture.Cell.Error is null && events.SequenceEqual(new[] { "Value", "Error" }),
                "Error recovery did not publish the current value and diagnostic state.");
        }
        finally { fixture.Cell.PropertyChanged -= observer; }
    }

    private static void DualFailure()
    {
        using var fixture = new Fixture();
        var writer = new InvalidOperationException("Setter failure");
        var observerError = new InvalidOperationException("Observer failure");
        fixture.WriteFailure = writer;
        PropertyChangedEventHandler observer = (_, args) =>
        {
            if (args.PropertyName == "Value") throw observerError;
        };
        fixture.Cell.PropertyChanged += observer;
        Exception? actual = null;
        try { fixture.Cell.Write(17); }
        catch (Exception error) { actual = error; }
        finally { fixture.Cell.PropertyChanged -= observer; fixture.WriteFailure = null; }
        Check(actual is AggregateException failures && failures.InnerExceptions.Count == 2 &&
            ReferenceEquals(failures.InnerExceptions[0], writer) && ReferenceEquals(failures.InnerExceptions[1], observerError),
            "Refresh masked the original setter exception or changed failure order.");
        Check(Equals(fixture.Cell.Value, 17), "Refresh did not observe the setter's partial mutation.");
        fixture.Cell.Write(42);
        Check(Equals(fixture.Cell.Value, 42) && fixture.Writes == 2, "Binding failed to recover after a dual failure.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Model[] Models = [new(1), new(2)];
        internal readonly FlatTreeDataGridSource<Model> Source;
        internal readonly TreeDataGridPresentation Presentation;
        internal readonly CellColumn Column;
        internal CellValue Cell;
        internal int Writes;
        internal Exception? WriteFailure;
        private bool _pooled;
        internal Fixture()
        {
            Source = new(Models);
            Source.Columns.Add(ValueColumn<Model, int>.FromDelegate("Value", model => model.Read(),
                propertyName: "Value", setter: (model, value) =>
                {
                    ++Writes;
                    model.Value = value;
                    if (WriteFailure is { } failure) throw failure;
                }));
            Presentation = TreeDataGridPresentation.Create(Source);
            Column = (CellColumn)Presentation.Columns[0];
            Cell = Presentation.RealizeCell(0, 0);
        }
        internal void RecycleOnly()
        {
            Presentation.RecycleCell(Column, Cell);
            _pooled = true;
        }
        internal void RecycleTo(int row)
        {
            RecycleOnly();
            Cell = Presentation.RealizeCell(0, row);
            _pooled = false;
        }
        public void Dispose()
        {
            try { if (!_pooled) Cell.Dispose(); }
            finally
            {
                try { Presentation.Dispose(); }
                finally { Source.Dispose(); }
            }
            Check(Models.All(model => model.Subscribers == 0), "The public consumer retained a model subscription.");
        }
    }

    private sealed class Model(int value) : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs ValueChanged = new("Value");
        private PropertyChangedEventHandler? _changed;
        internal int Value = value;
        internal Exception? Failure;
        internal int Subscribers;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
        internal int Read() { if (Failure is { } failure) throw failure; return Value; }
        internal void Notify() => _changed?.Invoke(this, ValueChanged);
    }

    private sealed class Input(Func<int> convert) : IConvertible
    {
        public int ToInt32(IFormatProvider? provider) => convert();
        public TypeCode GetTypeCode() => TypeCode.Object;
        public object ToType(Type type, IFormatProvider? provider) => throw new NotSupportedException();
        public bool ToBoolean(IFormatProvider? provider) => throw new NotSupportedException();
        public byte ToByte(IFormatProvider? provider) => throw new NotSupportedException();
        public char ToChar(IFormatProvider? provider) => throw new NotSupportedException();
        public DateTime ToDateTime(IFormatProvider? provider) => throw new NotSupportedException();
        public decimal ToDecimal(IFormatProvider? provider) => throw new NotSupportedException();
        public double ToDouble(IFormatProvider? provider) => throw new NotSupportedException();
        public short ToInt16(IFormatProvider? provider) => throw new NotSupportedException();
        public long ToInt64(IFormatProvider? provider) => throw new NotSupportedException();
        public sbyte ToSByte(IFormatProvider? provider) => throw new NotSupportedException();
        public float ToSingle(IFormatProvider? provider) => throw new NotSupportedException();
        public string ToString(IFormatProvider? provider) => throw new NotSupportedException();
        public ushort ToUInt16(IFormatProvider? provider) => throw new NotSupportedException();
        public uint ToUInt32(IFormatProvider? provider) => throw new NotSupportedException();
        public ulong ToUInt64(IFormatProvider? provider) => throw new NotSupportedException();
    }
}
