using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    // Cache syntax, not model owners or provider instances. Keys have binding lifetime.
    private static readonly ConditionalWeakTable<string, List<Segment>> s_paths = new();
    private readonly record struct Segment(string Name, bool Index);
    private readonly record struct Endpoint(Type Type, Func<object?> Read, Action<object?> Write);
    private static List<Segment> Parse(string path) => s_paths.GetValue(path, static value => ParseCore(value));
    private static List<Segment> ParseCore(string path)
    {
        var result = new List<Segment>();
        path = path.Trim();
        for (var i = 0; i < path.Length;)
        {
            if (result.Count > 0 && path[i] != '[')
            {
                if (path[i++] != '.') throw InvalidPath();
                while (i < path.Length && char.IsWhiteSpace(path[i])) ++i;
                if (i == path.Length || path[i] == '.') throw InvalidPath();
            }
            if (path[i] == '[')
            {
                var end = path.IndexOf(']', i + 1);
                if (end < 0) throw InvalidPath();
                var token = path[(i + 1)..end];
                if (token.Contains('[') || token.Contains('.')) throw InvalidPath();
                if (token.Contains('"') && (token.Length < 2 || token[0] != '"' || token[^1] != '"' ||
                    token.AsSpan(1, token.Length - 2).Contains('"'))) throw InvalidPath();
                result.Add(new(token, true));
                i = end + 1;
                while (i < path.Length && char.IsWhiteSpace(path[i])) ++i;
            }
            else
            {
                var start = i;
                while (i < path.Length && path[i] is not ('.' or '[')) ++i;
                var name = path[start..i].Trim();
                if (name.Length == 0 || name.Contains(']')) throw InvalidPath();
                result.Add(new(name, false));
            }
        }
        return result;
        ArgumentException InvalidPath() => new($"Unsupported or malformed binding write path '{path}'.", nameof(path));
    }
    private static string StringKey(string token) => token.Length >= 2 && token[0] == '"' && token[^1] == '"'
        ? token[1..^1] : token;
}
