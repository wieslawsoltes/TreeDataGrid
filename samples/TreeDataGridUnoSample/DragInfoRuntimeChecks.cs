using System;
using System.Collections.Generic;
using TreeDataGridCore;
using Uno.Controls.Models.TreeDataGrid;
using Windows.ApplicationModel.DataTransfer;

namespace TreeDataGridUnoSample;

/// <summary>Native data-package boundary checks, without manufacturing a live drag session.</summary>
internal static class DragInfoRuntimeChecks
{
    internal static void Run()
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Move };
        package.SetText("Ordinary external text must not become a row drag.");
        CheckRejected(package, "An unrelated native data package resolved as a row drag.");

        package.Properties[DragInfo.DataFormat] = 42;
        CheckRejected(package, "A non-string native property resolved as a row drag.");
        package.Properties[DragInfo.DataFormat] = string.Empty;
        CheckRejected(package, "An empty row-drag token resolved a source.");
        var token = Guid.NewGuid().ToString("N");
        package.Properties[DragInfo.DataFormat] = token;
        for (var iteration = 0; iteration < 32; ++iteration)
            CheckRejected(package, "An unknown row-drag token resolved a source.");

        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var indexes = new[] { new IndexPath(0) };
        var snapshot = new DragInfo(source, indexes);
        Check(ReferenceEquals(snapshot.Source, source) && ReferenceEquals(snapshot.Model, source) &&
            ReferenceEquals(snapshot.Indexes, indexes), "The public snapshot copied its Core source or supplied paths.");
        CheckRejected(package, "Constructing a public snapshot registered an unsolicited live drag.");

        // Reading one package must not turn a token into an implicitly trusted
        // object in another package, or consume unrelated native data properties.
        var copied = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        copied.Properties[DragInfo.DataFormat] = token;
        copied.Properties["TreeDataGrid.ExternalMarker"] = "preserved";
        CheckRejected(copied, "Copying an unknown native token resolved a source.");
        Check(Equals(copied.GetView().Properties["TreeDataGrid.ExternalMarker"], "preserved"),
            "Drag metadata lookup modified an unrelated package property.");
        Check(package.GetView().Contains(StandardDataFormats.Text), "Drag metadata lookup removed native text data.");
        Console.WriteLine("UNO_RUNTIME_DRAG_INFO_PASSED: public Core snapshot identity, native external/malformed/empty/unknown token rejection, repeated lookup, copied-token rejection and unrelated data preservation; live physical drag is a separate gate");
    }

    private static void CheckRejected(DataPackage package, string message) =>
        Check(!DragInfo.TryGet(package.GetView(), out var info) && info is null, message);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class Item { }
}
