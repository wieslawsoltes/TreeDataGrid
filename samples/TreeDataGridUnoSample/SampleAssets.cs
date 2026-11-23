using System;
using System.IO;
using System.Threading.Tasks;
#if !WINDOWS
using Windows.Storage;
#endif

namespace TreeDataGridUnoSample;

/// <summary>Reads files shipped with the sample (ms-appx:///path).</summary>
internal static class SampleAssets
{
    public static async Task<byte[]> ReadAllBytesAsync(string path)
    {
#if WINDOWS
        // Unpackaged Windows App SDK apps cannot resolve ms-appx through StorageFile; the assets
        // are copied next to the executable.
        return await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, path));
#else
        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///" + path));
        using var stream = await file.OpenStreamForReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
#endif
    }
}

/// <summary>Waits used by the runtime checks.</summary>
internal static class SampleWait
{
    /// <summary>
    /// Waits until a condition holds, for at most three seconds. WinUI raises Loaded/Unloaded, focus,
    /// view-changed and effective-viewport notifications after the layout pass (and later still when
    /// the window is not being rendered), so a fixed delay is not a reliable wait there. The caller
    /// asserts the condition afterwards.
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, Action? pump = null)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < 3000)
        {
            await Task.Delay(25);
            pump?.Invoke();
        }
    }
}
