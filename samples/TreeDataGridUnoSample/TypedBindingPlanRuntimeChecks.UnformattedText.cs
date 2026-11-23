using System;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static partial class TypedBindingPlanRuntimeChecks
{
    private static void VerifyUnformattedTextAndPolicies()
    {
        var definition = new TreeDataGridTextColumn();
        ColumnCreateOptions common = definition;
        Comparison<object?> compare = static (_, _) => 17;
        common.CanUserResize = false;
        common.CanUserSortColumn = true;
        common.AllowTriStateSorting = true;
        common.CompareAscending = compare;
        definition.CompareDescending = compare;
        common.BeginEditGestures = UI.BeginEditGestures.None;
        Check(definition.CanUserResize == false && definition.CanUserSortColumn == true && definition.AllowTriStateSorting &&
            ReferenceEquals(definition.CompareAscending, compare) && ReferenceEquals(common.CompareDescending, compare) &&
            definition.BeginEditGestures == UI.BeginEditGestures.None, "Declared definition policies diverged from base configuration.");
        definition.CompareAscending = null;
        Check(common.CompareAscending is null, "Definition comparison acquired independent storage.");

        var first = new Model { Number = 123456789 };
        var second = new Model { Number = 987654321 };
        using var source = new FlatTreeDataGridSource<Model>([first, second]);
        var core = new ValueColumn<Model, int>("Number", x => x.Number, (x, v) => x.Number = v);
        source.Columns.Add(core);
        using var native = new UI.TextColumn<Model, int>(core, new() { StringFormat = null! });
        using var immutable = new ValueCellColumn<Model, int>(core, CellKind.Text, new TextCellOptions { StringFormat = null! });
        using var a = native.CreateCell(source.Rows[0]);
        using var b = immutable.CreateCell(source.Rows[0]);
        Check(((UI.ITextCell)a).Text == "123456789" && ((UI.ITextCell)b).Text == "123456789" &&
            immutable.FormatValue(first.Number) == "123456789", "Null format failed to use the raw value contract.");
        Check(native.TryReuseCell(a, (IRow<Model>)source.Rows[1]), "Unformatted live cell could not retarget.");
        first.Number = 111;
        Check(((UI.ITextCell)a).Text == "987654321" && ((UI.ITextCell)b).Text == "111",
            "Unformatted text read an old or unrelated binding owner.");
        a.Write(222); b.Write(333);
        Check(second.Number == 222 && first.Number == 333, "Unformatted cells changed writeback ownership.");
        native.Options.StringFormat = "[{0}]";
        Check(((UI.ITextCell)a).Text == "[222]", "Enabling a format did not update the retained model.");
        native.Options.StringFormat = null!;
        Check(((UI.ITextCell)a).Text == "222", "Disabling the format did not restore raw display.");
        a.Dispose(); b.Dispose();
        Check(first.Subscribers == 0 && second.Subscribers == 0 && source.Rows.Count == 2,
            "Raw text retirement leaked observers or disposed the caller's Core source.");
        Console.WriteLine("UNO_RUNTIME_UNFORMATTED_TEXT_POLICIES_PASSED: one policy store, null-format raw values, independent Core owners, retained retarget, writeback, live format switching and cleanup");
    }
}
