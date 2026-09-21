using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreeDataGridUnoSamples;

/// <summary>One launch/result contract for native processes and browser hosts.</summary>
internal static class SampleRunContext
{
    internal static string[] Arguments { get; private set; } = OperatingSystem.IsBrowser()
        ? Array.Empty<string>() : Environment.GetCommandLineArgs();
    internal static Action<bool, string?>? ResultSink { get; set; }
    private static bool _failed;
    internal static bool HasArgument(string argument) => Arguments.Contains(argument, StringComparer.Ordinal);
    internal static void InitializeBrowser(string query)
    {
        Arguments = ParseBrowserArguments(query);
        _failed = false;
    }
    internal static string[] ParseBrowserArguments(string query)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = parameter.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var value = parts.Length == 1 ? "1" : Uri.UnescapeDataString(parts[1]).ToLowerInvariant();
            // Query strings are launch options, never arbitrary process arguments,
            // file output locations, source paths or JavaScript fragments.
            if ((key is "smoke" or "demo" or "offline" or "wikipedia-live") && (value is "" or "1" or "true")) result.Add("--" + key);
        }
        return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
    internal static void ReportResult(bool passed, string? error = null)
    {
        if (_failed && passed) return;
        _failed |= !passed;
        ResultSink?.Invoke(passed, error);
    }
    internal static string SerializeResult(bool passed, string? error) => JsonSerializer.Serialize(
        new SampleRunResult(true, passed, error), SampleRunJsonContext.Default.SampleRunResult);
}

internal sealed record SampleRunResult(bool complete, bool passed, string? error);

[JsonSerializable(typeof(SampleRunResult))]
internal partial class SampleRunJsonContext : JsonSerializerContext { }
