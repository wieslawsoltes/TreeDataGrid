using System;
using Microsoft.UI.Xaml.Automation.Peers;

namespace TreeDataGridUnoSample;

internal static class RuntimeAssertions
{
    internal static T Pattern<T>(AutomationPeer peer, PatternInterface pattern) where T : class =>
        peer.GetPattern(pattern) as T ?? throw new InvalidOperationException(
            $"{peer.GetType().Name} did not expose required {pattern} pattern ({typeof(T).Name}).");
}
