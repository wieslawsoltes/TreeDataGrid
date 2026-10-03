using System;
using System.IO;

namespace TreeDataGridUnoSample;

internal static class BrowserFileSample
{
    private static string? _directory;
    internal static string InitialDirectory()
    {
        if (!OperatingSystem.IsBrowser()) return Directory.GetCurrentDirectory();
        if (_directory is not null) return _directory;
        // These are sample-owned files in the browser's in-memory sandbox.
        // They never access or modify the user's host filesystem.
        var directory = Directory.CreateTempSubdirectory("TreeDataGrid-browser-demo-");
        Directory.CreateDirectory(Path.Combine(directory.FullName, "Documents"));
        Directory.CreateDirectory(Path.Combine(directory.FullName, "Empty folder"));
        File.WriteAllText(Path.Combine(directory.FullName, "Read me.txt"),
            "TreeDataGrid browser sandbox sample. Open / refresh reads a new snapshot. Host filesystem watchers are unavailable.");
        File.WriteAllText(Path.Combine(directory.FullName, "Documents", "Example.txt"), "A lazily expanded shared file model.");
        return _directory = directory.FullName;
    }
}
