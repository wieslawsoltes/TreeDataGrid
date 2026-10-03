using System;
using System.Globalization;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Experimental.Data;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static partial class TypedBindingPlanRuntimeChecks
{
    private static void VerifyNumericFormatting()
    {
        var previous = CultureInfo.CurrentCulture;
        var first = new Model { Number = 123456789 };
        var second = new Model { Number = 987654321 };
        using var source = new FlatTreeDataGridSource<Model>([first, second]);
        var definition = new ValueColumn<Model, int>("Number", x => x.Number, (x, value) => x.Number = value);
        source.Columns.Add(definition);
        using var nativeColumn = new UI.TextColumn<Model, int>(definition,
            new UI.TextColumnOptions<Model> { Culture = null! });
        using var coreColumn = new ValueCellColumn<Model, int>(definition, CellKind.Text,
            new TextCellOptions { Culture = null! });
        var descriptor = TypedBinding<Model>.TwoWay(x => x.Number, (x, value) => x.Number = value);
        using var expression = descriptor.Instance(first);
        var options = new UI.TextColumnOptions<Model> { Culture = null! };
        using var scalar = new UI.TextCell<int>(expression, false, options);
        using var native = nativeColumn.CreateCell(source.Rows[0]);
        using var core = coreColumn.CreateCell(source.Rows[0]);
        var nativeText = (UI.ITextCell)native;
        var coreText = (UI.ITextCell)core;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Check(scalar.Text == "123456789" && nativeText.Text == scalar.Text && coreText.Text == scalar.Text,
                "Numeric text differs between public scalar, live column and immutable view models.");
            var custom = new NumericCulture { Prefix = "first:" };
            CultureInfo.CurrentCulture = custom;
            Check(scalar.Text == "first:123456789" && nativeText.Text == scalar.Text && coreText.Text == scalar.Text,
                "Null display culture bypassed the current culture's custom formatter.");
            custom.Prefix = "next:";
            first.Number = 42;
            Check(scalar.Text == "next:42" && nativeText.Text == scalar.Text && coreText.Text == scalar.Text,
                "Live numeric updates or provider mutations reused a stale formatted result.");
            scalar.BeginEdit();
            scalar.Text = "54321";
            Check(scalar.Text == "54321", "Display formatting altered buffered editor text.");
            scalar.EndEdit();
            Check(first.Number == 54321 && nativeText.Text == "next:54321" && coreText.Text == nativeText.Text,
                "Numeric editing failed to update the actual shared Core row.");
            var failure = new InvalidOperationException("expected formatter failure");
            custom.Failure = failure;
            Exception? actual = null;
            try { _ = nativeText.Text; } catch (Exception error) { actual = error; }
            Check(ReferenceEquals(actual, failure), "Custom formatter exception identity changed.");
            custom.Failure = null;
            Check(nativeText.Text == "next:54321", "Formatter failure prevented later recovery.");
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Check(nativeColumn.TryReuseCell(native, (IRow<Model>)source.Rows[1]) && nativeText.Text == "987654321" &&
                Equals(native.Value, second.Number), "Typed formatting lost the current value after retained reuse.");
            options.StringFormat = "[{0:N2}]";
            Check(scalar.Text == string.Format(CultureInfo.InvariantCulture, options.StringFormat, first.Number),
                "Nonidentity numeric formatting stopped using the ordinary composite-format contract.");
        }
        finally { CultureInfo.CurrentCulture = previous; }
        scalar.Dispose(); expression.Dispose(); native.Dispose(); core.Dispose();
        Check(first.Subscribers == 0 && second.Subscribers == 0 && source.Rows.Count == 2,
            "Numeric formatting or recovery changed binding/source ownership.");
        Console.WriteLine("UNO_RUNTIME_NUMERIC_FORMATTING_PASSED: scalar/live/immutable text models, custom current culture, live values, editing, original errors, recovery, retained reuse and shared Core cleanup");
    }

    private sealed class NumericCulture() : CultureInfo("en-US"), ICustomFormatter
    {
        internal string Prefix = "custom:";
        internal Exception? Failure;
        public override object? GetFormat(Type? type) => type == typeof(ICustomFormatter) ? this : base.GetFormat(type);
        public string Format(string? format, object? value, IFormatProvider? provider)
        {
            if (Failure is { } error) throw error;
            return Prefix + value;
        }
    }
}
