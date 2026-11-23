using System;
using System.Collections.Generic;
using System.Globalization;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class UnformattedBoundTextParityTests
{
    public static IEnumerable<object[]> Values()
    {
        for (var route = 0; route < 3; ++route)
            for (var kind = 0; kind < 9; ++kind)
                yield return new object[] { route, kind };
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void Unformatted_bound_text_matches_the_original_scalar_ToString_contract(int route, int kind)
    {
        switch (kind)
        {
            case 0: Check(route, 123456789); break;
            case 1: Check(route, -1234.5); break;
            case 2: Check(route, 1234.567m); break;
            case 3: Check<int?>(route, 17); break;
            case 4: Check<int?>(route, null); break;
            case 5: Check(route, "unchanged string"); break;
            case 6: Check<string>(route, null); break;
            case 7: Check(route, DayOfWeek.Saturday); break;
            default: Check<object>(route, new TextObject()); break;
        }
    }

    private static void Check<T>(int route, T? value)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var reference = new A.TextCell<T>(value);
            var row = new Model<T> { Value = value };
            using var source = new FlatTreeDataGridSource<Model<T>>([row]);
            var definition = new ValueColumn<Model<T>, T?>("Value", x => x.Value);
            source.Columns.Add(definition);
            using CellColumn column = route == 0
                ? new U.TextColumn<Model<T>, T>(definition, new() { StringFormat = null!, Culture = new RejectFormattingCulture() })
                : new ValueCellColumn<Model<T>, T?>(definition, CellKind.Text,
                    route == 1 ? null : new TextCellOptions { StringFormat = null!, Culture = new RejectFormattingCulture() });
            using var cell = column.CreateCell(source.Rows[0]);
            Assert.Equal(reference.Text, ((U.ITextCell)cell).Text);
            Assert.Equal(reference.Text ?? string.Empty, column.FormatValue(value));
            Assert.Equal(value, cell.Value);
            // This format opt-out must keep following CurrentCulture, not the
            // explicitly supplied (and deliberately unusable) display provider.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(reference.Text, ((U.ITextCell)cell).Text);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private sealed class Model<T> { public T? Value { get; set; } }
    private sealed class TextObject
    {
        public override string ToString() => "custom object";
    }
    private sealed class RejectFormattingCulture() : CultureInfo("en-US")
    {
        public override object? GetFormat(Type? type) => throw new InvalidOperationException("Unformatted text consulted the options provider.");
    }
}
