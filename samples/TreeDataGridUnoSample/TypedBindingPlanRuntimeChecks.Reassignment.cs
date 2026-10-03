using System;
using Uno.Data;
using Uno.Experimental.Data;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static partial class TypedBindingPlanRuntimeChecks
{
    private static void VerifySameAssignments()
    {
        var a = new Model { Number = 17 }; var b = new Model { Number = 29 };
        var descriptor = TypedBinding<Model>.TwoWay(x => x.Number, (x, value) => x.Number = value);
        var read = descriptor.Read;
        var write = descriptor.Write;
        var links = descriptor.Links!;
        using var original = descriptor.Instance(a, BindingMode.TwoWay);
        using var originalCell = new UI.TextCell<int>(original, false);
        for (var iteration = 0; iteration < 32; ++iteration)
        {
            descriptor.Read = read;
            descriptor.Write = write;
            descriptor.Links = links;
            using (var next = descriptor.Instance(b, BindingMode.TwoWay))
            using (var cell = new UI.TextCell<int>(next, false))
            {
                cell.Value = 100 + iteration;
                Check(b.Number == 100 + iteration && originalCell.Value == 17 && a.Number == 17,
                    "Reassigned instructions wrote to the retained expression's root.");
                Check(a.Subscribers == 1 && b.Subscribers == 1,
                    "Repeated assignments duplicated or omitted expression observations.");
            }
            Check(a.Subscribers == 1 && b.Subscribers == 0,
                "Disposal of a reassigned expression retired the old expression or leaked its current root.");
        }

        var link = links[0];
        links[0] = null!;
        descriptor.Links = links;
        Exception? failure = null;
        try { using var rejected = descriptor.Instance(b); }
        catch (Exception error) { failure = error; }
        Check(failure is ArgumentException, "Same-array reassignment bypassed link validation.");
        a.Number = 43;
        Check(originalCell.Value == 43, "Failed reassignment damaged an existing expression snapshot.");
        links[0] = link;
        descriptor.Links = links;
        using (var repaired = descriptor.Instance(b))
        using (var cell = new UI.TextCell<int>(repaired, false))
            Check(cell.Value == b.Number, "A repaired reassignment did not recover its live value.");

        descriptor.Write = null;
        descriptor.Write = null;
        failure = null;
        try { using var invalid = descriptor.Instance(b, BindingMode.TwoWay); }
        catch (Exception error) { failure = error; }
        Check(failure is InvalidOperationException, "Reassignment bypassed the required writer contract.");
        originalCell.Value = 61;
        Check(a.Number == 61, "Descriptor mutation replaced the existing expression's writer.");
        originalCell.Dispose(); original.Dispose();
        Check(a.Subscribers == 0 && b.Subscribers == 0, "Reassignment consumer leaked model observers.");
        Console.WriteLine("UNO_RUNTIME_BINDING_REASSIGNMENT_PASSED: repeated public assignments, independent native text values/writeback, same-array validation, repair, required writer and old-snapshot cleanup");
    }
}
