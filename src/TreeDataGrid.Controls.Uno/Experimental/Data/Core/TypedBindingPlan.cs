using System;

namespace Uno.Experimental.Data.Core;

/// <summary>Snapshot helpers; no global plan cache, root, or observation storage.</summary>
internal static class TypedBindingPlan<TIn, TOut> where TIn : class
{
    internal static Func<TIn, object?>[] CopyLinks(Func<TIn, object>[] links)
    {
        var copy = new Func<TIn, object?>[links.Length];
        for (var i = 0; i < links.Length; ++i)
            copy[i] = links[i] ?? throw new ArgumentException("A binding link cannot be null.", nameof(links));
        return copy;
    }

    internal static bool MatchesLinks(Func<TIn, object>[] links, Func<TIn, object?>[] snapshot)
    {
        if (links.Length != snapshot.Length) return false;
        // Element identity matters: callers can modify Links without its setter.
        // Neither delegate equality nor arbitrary model equality is invoked.
        for (var i = 0; i < links.Length; ++i)
            if (!ReferenceEquals(links[i], snapshot[i])) return false;
        return true;
    }
}
